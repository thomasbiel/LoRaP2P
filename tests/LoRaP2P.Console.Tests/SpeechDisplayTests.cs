using System.Globalization;
using LoRaP2P.SpeechDisplay;
using LoRaP2P.Display.RadioBonnet;

namespace LoRaP2P.Console.Tests;

[TestFixture]
public sealed class SpeechDisplayTests
{
    private static readonly string[] ExpectedScrolled = ["two", "three", "four", "five"];
    private static readonly string[] ExpectedFirst = ["first sentence"];
    private static readonly string[] ExpectedBacklog = ["44"];

    [TestCase(false)]
    [TestCase(true)]
    [NonParallelizable]
    public async Task CliUsesEnglishUiLanguageWithoutChangingNumericCulture(bool speechTool)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var previousOutput = System.Console.Out;
        var previousError = System.Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.DefaultThreadCurrentUICulture = null;
            System.Console.SetOut(output);
            System.Console.SetError(error);

            var exitCode = speechTool
                ? await LoRaP2P.SpeechDisplay.Program.Main(["--help"])
                : await LoRaP2P.Console.Program.Main(["--help"]);
            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(CultureInfo.DefaultThreadCurrentUICulture?.Name, Is.EqualTo("en-US"));
                Assert.That(CultureInfo.CurrentCulture.Name, Is.EqualTo("de-DE"));
                Assert.That(output.ToString() + error, Does.Contain("--help"));
            });
        }
        finally
        {
            System.Console.SetOut(previousOutput);
            System.Console.SetError(previousError);
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultUiCulture;
        }
    }

    [Test]
    public void OptionsDefaultToBundledLibraryWithoutRequiringOverride()
    {
        using var parser = new CommandLine.Parser(settings => settings.HelpWriter = TextWriter.Null);
        var result = parser.ParseArguments<SpeechDisplayOptions>(["--model", "model-directory"]);
        Assert.That(result, Is.InstanceOf<CommandLine.Parsed<SpeechDisplayOptions>>());
        var options = ((CommandLine.Parsed<SpeechDisplayOptions>)result).Value;
        Assert.That(options.ResolvedVoskLibraryPath,
            Is.EqualTo(Path.Combine(AppContext.BaseDirectory, "libvosk.so")));
    }

    [Test]
    public void OptionsResolveExplicitNativeLibraryOverride()
    {
        var path = Path.Combine("native", "libvosk.so");
        var options = new SpeechDisplayOptions { VoskLibraryPath = path };
        Assert.That(options.ResolvedVoskLibraryPath, Is.EqualTo(Path.GetFullPath(path)));
    }

    [Test]
    public void AdvanceScrollsUpAndKeepsLatestFourLines()
    {
        var buffer = new SpeechScrollBuffer();
        buffer.Append("one\ntwo\nthree\nfour\nfive");

        for (var index = 0; index < 4; index++)
        {
            buffer.Advance();
        }

        Assert.That(buffer.Advance(), Is.EqualTo(ExpectedScrolled));
        Assert.That(buffer.Advance(), Is.Null);
    }

    [Test]
    public void AppendPreservesPreviouslyQueuedSpeechAndWrapsLongWords()
    {
        var buffer = new SpeechScrollBuffer();
        buffer.Append("first sentence");
        buffer.Append(new string('x', 25));

        Assert.That(buffer.Advance(), Is.EqualTo(ExpectedFirst));
        Assert.That(buffer.Advance(), Is.EqualTo(new[] { "first sentence", new string('x', 21) }));
        Assert.That(buffer.Advance(), Is.EqualTo(new[] { "first sentence", new string('x', 21), "xxxx" }));
    }

    [Test]
    public void AppendIgnoresSilenceAndBoundsBacklog()
    {
        var buffer = new SpeechScrollBuffer();
        buffer.Append("  ");
        Assert.That(buffer.Advance(), Is.Null);

        for (var index = 0; index < 300; index++)
        {
            buffer.Append(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.That(buffer.Advance(), Is.EqualTo(ExpectedBacklog));
    }

    [TestCase("{\"text\":\"hello world\"}", "hello world")]
    [TestCase("{\"text\":\"caf\\u00e9 menu\"}", "caf\u00e9 menu")]
    [TestCase("{\"text\":\"\"}", "")]
    [TestCase("{\"partial\":\"hello\"}", "")]
    public void ReadTextOnlyReturnsConfirmedUtf8Speech(string json, string expected)
    {
        Assert.That(MicrophoneTranscriber.ReadText(json), Is.EqualTo(expected));
    }

    [Test]
    public void MicrophoneUsesRawMonoPcmAndDoesNotInvokeShell()
    {
        var startInfo = MicrophoneTranscriber.CreateStartInfo("plughw:CARD=USB,DEV=0");
        Assert.Multiple(() =>
        {
            Assert.That(startInfo.FileName, Is.EqualTo("arecord"));
            Assert.That(startInfo.UseShellExecute, Is.False);
            Assert.That(startInfo.RedirectStandardOutput, Is.True);
            Assert.That(startInfo.RedirectStandardError, Is.True);
            Assert.That(startInfo.Environment["LC_ALL"], Is.EqualTo("C"));
            Assert.That(startInfo.ArgumentList, Does.Contain("plughw:CARD=USB,DEV=0"));
            Assert.That(startInfo.ArgumentList, Does.Contain("S16_LE"));
            Assert.That(startInfo.ArgumentList, Does.Contain("16000"));
            Assert.That(startInfo.ArgumentList, Does.Contain("raw"));
            Assert.That(startInfo.ArgumentList, Does.Contain("1"));
        });
    }

    [Test]
    public void FrameBufferClipsTextAndClearsPreviousFrame()
    {
        var frame = new RadioBonnetFrameBuffer();
        frame.Render([new string('A', 30), "B", "C", "D", "E"]);
        Assert.Multiple(() =>
        {
            Assert.That(frame.Pixels, Has.Length.EqualTo(512));
            Assert.That(frame.Pixels[126], Is.Zero);
            Assert.That(frame.Pixels[128], Is.EqualTo(0x7F));
            Assert.That(frame.Pixels[384], Is.EqualTo(0x7F));
        });

        frame.Render([]);
        Assert.That(frame.Pixels, Is.All.Zero);
    }

    [Test]
    public void FrameBufferUsesQuestionMarkForUnsupportedCharacter()
    {
        var frame = new RadioBonnetFrameBuffer();
        frame.Render(["\u2603"]);
        Assert.That(frame.Pixels.Take(5), Is.EqualTo(RadioBonnetFont.Glyphs['?']));
    }

    [TestCase(1)]
    [TestCase(3)]
    [TestCase(4095)]
    public async Task PcmReadsPreserveCompleteSamplesAcrossFragmentedReads(int fragmentSize)
    {
        var expected = Enumerable.Range(0, 5000).Select(index => (byte)(index % 256)).ToArray();
        using var audio = new FragmentedAudioStream(expected, fragmentSize);
        List<byte> received = [];
        await MicrophoneTranscriber.ReadPcmAsync(audio, (buffer, count) =>
        {
            Assert.That(count % 2, Is.Zero);
            received.AddRange(buffer.Take(count));
        }, CancellationToken.None);

        Assert.That(received, Is.EqualTo(expected));
    }

    [Test]
    public async Task PcmReadsRejectTruncatedSample()
    {
        using var audio = new MemoryStream([0, 1, 2]);
        await Assert.ThrowsAsync<IOException>(() =>
            MicrophoneTranscriber.ReadPcmAsync(audio, (_, _) => { }, CancellationToken.None));
    }

    [Test]
    public async Task PcmReadsHonorCancellation()
    {
        using var audio = new MemoryStream([0, 1]);
        await Assert.CatchAsync<OperationCanceledException>(() =>
            MicrophoneTranscriber.ReadPcmAsync(audio, (_, _) => Assert.Fail("Unexpected samples."),
                new CancellationToken(canceled: true)));
    }

    [Test]
    public async Task CaptureExitDuringCancellationDoesNotReportMicrophoneFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var handling = MicrophoneTranscriber.HandleCaptureExitAsync(1,
            "arecord: pcm_read:2272: read error: Interrupted system call", cancellation.Token);
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(() => handling);
    }

    [Test]
    public async Task UnexpectedCaptureExitPreservesUsefulMicrophoneDiagnostics()
    {
        var exception = await Assert.ThrowsAsync<IOException>(() =>
            MicrophoneTranscriber.HandleCaptureExitAsync(1, "arecord: Device or resource busy",
                CancellationToken.None));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.Message, Does.Contain("selected ALSA device is available"));
        Assert.That(exception.Message, Does.Contain("Device or resource busy"));
    }

    private sealed class FragmentedAudioStream(byte[] audio, int fragmentSize) : MemoryStream(audio)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, fragmentSize)], cancellationToken);
    }
}