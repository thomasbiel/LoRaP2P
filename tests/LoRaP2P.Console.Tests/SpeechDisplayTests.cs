using LoRaP2P.SpeechDisplay;
using LoRaP2P.Display.RadioBonnet;

namespace LoRaP2P.Console.Tests;

[TestFixture]
public sealed class SpeechDisplayTests
{
    private static readonly string[] ExpectedScrolled = ["zwei", "drei", "vier", "fuenf"];
    private static readonly string[] ExpectedFirst = ["erster Satz"];
    private static readonly string[] ExpectedBacklog = ["44"];

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
        buffer.Append("eins\nzwei\ndrei\nvier\nfuenf");

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
        buffer.Append("erster Satz");
        buffer.Append(new string('x', 25));

        Assert.That(buffer.Advance(), Is.EqualTo(ExpectedFirst));
        Assert.That(buffer.Advance(), Is.EqualTo(new[] { "erster Satz", new string('x', 21) }));
        Assert.That(buffer.Advance(), Is.EqualTo(new[] { "erster Satz", new string('x', 21), "xxxx" }));
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

    [TestCase("{\"text\":\"hallo welt\"}", "hallo welt")]
    [TestCase("{\"text\":\"gr\\u00fcne wiese\"}", "gr\u00fcne wiese")]
    [TestCase("{\"text\":\"\"}", "")]
    [TestCase("{\"partial\":\"hallo\"}", "")]
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

    private sealed class FragmentedAudioStream(byte[] audio, int fragmentSize) : MemoryStream(audio)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, fragmentSize)], cancellationToken);
    }
}