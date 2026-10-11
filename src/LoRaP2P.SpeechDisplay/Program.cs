using CommandLine;
using LoRaP2P.Display.RadioBonnet;
using Vosk;

namespace LoRaP2P.SpeechDisplay;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var parser = new Parser(settings => settings.HelpWriter = System.Console.Out);
        var parsed = parser.ParseArguments<SpeechDisplayOptions>(args);
        return await parsed.MapResult(RunAsync, errors => Task.FromResult(
            errors.All(error => error is HelpRequestedError or VersionRequestedError) ? 0 : 2))
            .ConfigureAwait(false);
    }

    private static async Task<int> RunAsync(SpeechDisplayOptions options)
    {
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };
        System.Console.CancelKeyPress += cancelHandler;
        try
        {
            options.Validate();
            if (!OperatingSystem.IsLinux())
            {
                System.Console.Error.WriteLine("Mikrofonaufnahme benoetigt Linux mit ALSA/arecord.");
                return 1;
            }

            MicrophoneTranscriber.ConfigureNativeLibrary(options.ResolvedVoskLibraryPath);
            Vosk.Vosk.SetLogLevel(-1);
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            using var downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            downloadTimeout.CancelAfter(TimeSpan.FromMinutes(30));
            var models = new VoskModelStore(httpClient, VoskModelStore.UserModelsDirectory);
            var modelPath = await models.GetModelAsync(options.ModelPath, options.ModelUrl,
                System.Console.Out, downloadTimeout.Token).ConfigureAwait(false);
            using var model = new Model(modelPath);
            using var recognizer = new VoskRecognizer(model, MicrophoneTranscriber.SampleRate);
            using var display = options.NoDisplay ? null : new RadioBonnetDisplay();
            var buffer = new SpeechScrollBuffer();
            using var session = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);
            System.Console.WriteLine($"Mikrofon: {options.Device}; 16000 Hz, Mono. Beenden mit Ctrl+C.");
            var scrolling = ScrollAsync(buffer, display, options.ScrollMilliseconds, session.Token);
            var recording = MicrophoneTranscriber.RunAsync(options.Device, recognizer, text =>
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    System.Console.WriteLine(text);
                    buffer.Append(text);
                }
            }, session.Token);

            try
            {
                var completed = await Task.WhenAny(recording, scrolling).ConfigureAwait(false);
                await completed.ConfigureAwait(false);
            }
            finally
            {
                await session.CancelAsync().ConfigureAwait(false);
                try
                {
                    await Task.WhenAll(recording, scrolling).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (session.IsCancellationRequested)
                {
                }
            }

            return 0;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine($"Fehler: {exception.Message}");
            System.Console.Error.WriteLine("ALSA-Mikrofon, Vosk-Modell/Bibliotheksarchitektur und I2C-/GPIO-Rechte pruefen.");
            return 1;
        }
        finally
        {
            System.Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task ScrollAsync(
        SpeechScrollBuffer buffer,
        RadioBonnetDisplay? display,
        int intervalMilliseconds,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMilliseconds));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (buffer.Advance() is { } lines)
            {
                display?.ShowLines(lines);
            }
        }
    }
}