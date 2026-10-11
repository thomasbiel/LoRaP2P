using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Vosk;

namespace LoRaP2P.SpeechDisplay;

internal static class MicrophoneTranscriber
{
    public const int SampleRate = 16000;

    public static void ConfigureNativeLibrary(string libraryPath)
    {
        var fullPath = Path.GetFullPath(libraryPath);
        NativeLibrary.SetDllImportResolver(typeof(Model).Assembly,
            (name, _, _) => StringComparer.Ordinal.Equals(name, "libvosk")
                ? NativeLibrary.Load(fullPath)
                : IntPtr.Zero);
    }

    public static ProcessStartInfo CreateStartInfo(string device)
    {
        var startInfo = new ProcessStartInfo("arecord")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.Environment["LC_ALL"] = "C";
        foreach (var argument in new[] { "--quiet", "--device", device, "--file-type", "raw",
            "--format", "S16_LE", "--channels", "1", "--rate",
            SampleRate.ToString(CultureInfo.InvariantCulture), "-" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static string ReadText(string result)
    {
        using var document = JsonDocument.Parse(result);
        return document.RootElement.TryGetProperty("text", out var text)
            && text.ValueKind == JsonValueKind.String
                ? text.GetString() ?? ""
                : "";
    }

    public static async Task RunAsync(
        string device,
        VoskRecognizer recognizer,
        Action<string> onText,
        CancellationToken cancellationToken)
    {
        using var microphone = new Process { StartInfo = CreateStartInfo(device) };
        microphone.Start();
        var errors = RelayErrorsAsync(microphone.StandardError);
        try
        {
            await ReadPcmAsync(microphone.StandardOutput.BaseStream, (buffer, count) =>
            {
                if (recognizer.AcceptWaveform(buffer, count))
                {
                    onText(ReadText(recognizer.Result()));
                }
            }, cancellationToken).ConfigureAwait(false);

            onText(ReadText(recognizer.FinalResult()));
            await microphone.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            throw new IOException($"Microphone capture stopped unexpectedly (arecord exit code {microphone.ExitCode}).");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            onText(ReadText(recognizer.FinalResult()));
            throw;
        }
        finally
        {
            try
            {
                if (!microphone.HasExited)
                {
                    microphone.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException) when (microphone.HasExited)
            {
            }

            await microphone.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await errors.ConfigureAwait(false);
        }
    }

    internal static async Task ReadPcmAsync(
        Stream audio,
        Action<byte[], int> onSamples,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        while (true)
        {
            var count = await audio.ReadAtLeastAsync(buffer, 2, throwOnEndOfStream: false,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (count == 0)
            {
                return;
            }

            if (count % 2 != 0)
            {
                var lastByte = await audio.ReadAsync(buffer.AsMemory(count, 1), cancellationToken)
                    .ConfigureAwait(false);
                if (lastByte == 0)
                {
                    throw new IOException("The microphone returned an incomplete 16-bit PCM sample.");
                }

                count++;
            }

            onSamples(buffer, count);
        }
    }

    private static async Task RelayErrorsAsync(StreamReader errorReader)
    {
        while (await errorReader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            await System.Console.Error.WriteLineAsync(line).ConfigureAwait(false);
        }
    }
}