using System.IO.Compression;
using System.Net;
using LoRaP2P.SpeechDisplay;

namespace LoRaP2P.Console.Tests;

[TestFixture]
internal sealed class VoskModelStoreTests
{
    private string directory = "";

    [SetUp]
    public void SetUp()
    {
        this.directory = Path.Combine(Path.GetTempPath(), $"vosk-model-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(this.directory, recursive: true);

    [Test]
    public void OptionsAllowAutomaticDefaultModel()
    {
        using var parser = new CommandLine.Parser(settings => settings.HelpWriter = TextWriter.Null);
        var parsed = parser.ParseArguments<SpeechDisplayOptions>([]);
        Assert.That(parsed, Is.InstanceOf<CommandLine.Parsed<SpeechDisplayOptions>>());
        var options = ((CommandLine.Parsed<SpeechDisplayOptions>)parsed).Value;
        Assert.That(options.ModelUrl, Is.EqualTo(VoskModelStore.DefaultModelUrl));
        Assert.That(options.ModelPath, Is.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DownloadExtractsModelAndReusesCache(bool nested)
    {
        using var handler = new ArchiveHandler(CreateModelArchive(nested));
        using var client = new HttpClient(handler);
        using var output = new StringWriter();
        var store = new VoskModelStore(client, this.directory);
        var model = await store.GetModelAsync("", VoskModelStore.DefaultModelUrl,
            output, CancellationToken.None);
        var cached = await store.GetModelAsync("", VoskModelStore.DefaultModelUrl,
            output, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(VoskModelStore.IsModelDirectory(model), Is.True);
            Assert.That(cached, Is.EqualTo(model));
            Assert.That(handler.RequestCount, Is.EqualTo(1));
            Assert.That(output.ToString(), Does.Contain("Downloading speech model:"));
            Assert.That(output.ToString(), Does.Contain("Extracting speech model..."));
            Assert.That(output.ToString(), Does.Contain("Speech model ready:"));
            Assert.That(output.ToString(), Does.Contain("Using cached speech model:"));
            Assert.That(Directory.GetDirectories(this.directory, ".download-*"), Is.Empty);
            Assert.That(Directory.GetFiles(this.directory, "*.zip", SearchOption.AllDirectories), Is.Empty);
        });
    }

    [Test]
    public async Task FailedDownloadDoesNotLeaveCacheOrTemporaryFiles()
    {
        using var handler = new ArchiveHandler([], HttpStatusCode.NotFound);
        using var client = new HttpClient(handler);
        var store = new VoskModelStore(client, this.directory);
        await Assert.ThrowsAsync<HttpRequestException>(() => store.GetModelAsync("",
            VoskModelStore.DefaultModelUrl, TextWriter.Null, CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(this.directory), Is.Empty);
    }

    [Test]
    public async Task LocalModelBypassesNetworkAndIgnoresDownloadUrl()
    {
        using (var data = new MemoryStream(CreateModelArchive()))
        using (var archive = new ZipArchive(data))
        {
            archive.ExtractToDirectory(this.directory);
        }

        using var handler = new ArchiveHandler([]);
        using var client = new HttpClient(handler);
        var store = new VoskModelStore(client, this.directory);
        var local = Path.Combine(this.directory, "test-model");
        var model = await store.GetModelAsync(local, "unused-url", TextWriter.Null, CancellationToken.None);
        Assert.That(model, Is.EqualTo(local));
        Assert.That(handler.RequestCount, Is.Zero);
    }

    [Test]
    public async Task DifferentModelUrlsUseSeparateCaches()
    {
        using var handler = new ArchiveHandler(CreateModelArchive());
        using var client = new HttpClient(handler);
        var store = new VoskModelStore(client, this.directory);
        var first = await store.GetModelAsync("", VoskModelStore.DefaultModelUrl,
            TextWriter.Null, CancellationToken.None);
        var second = await store.GetModelAsync("", "https://example.org/other-model.zip",
            TextWriter.Null, CancellationToken.None);
        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(handler.RequestCount, Is.EqualTo(2));
    }

    [TestCase("http://example.org/model.zip")]
    [TestCase("file:///model.zip")]
    [TestCase("https://user:password@example.org/model.zip")]
    [TestCase("https://example.org/model.zip#fragment")]
    public void InvalidModelUrlIsRejected(string url) =>
        Assert.Throws<ArgumentException>(() => VoskModelStore.ParseModelUrl(url));

    [Test]
    public async Task CancellationDuringDownloadRemovesTemporaryFiles()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new ArchiveHandler(CreateModelArchive()) { OnRequest = cancellation.Cancel };
        using var client = new HttpClient(handler);
        var store = new VoskModelStore(client, this.directory);
        await Assert.CatchAsync<OperationCanceledException>(() => store.GetModelAsync("",
            VoskModelStore.DefaultModelUrl, TextWriter.Null, cancellation.Token));
        Assert.That(Directory.GetFileSystemEntries(this.directory), Is.Empty);
    }

    [Test]
    public async Task InvalidZipDoesNotLeavePublishedModel()
    {
        using var handler = new ArchiveHandler([1, 2, 3]);
        using var client = new HttpClient(handler);
        var store = new VoskModelStore(client, this.directory);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetModelAsync("",
            VoskModelStore.DefaultModelUrl, TextWriter.Null, CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(this.directory), Is.Empty);
    }

    [Test]
    public async Task ArchiveCannotWriteOutsideExtractionDirectory()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("../escaped.txt");
        }

        using var handler = new ArchiveHandler(buffer.ToArray());
        using var client = new HttpClient(handler);
        var store = new VoskModelStore(client, this.directory);
        await Assert.ThrowsAsync<IOException>(() => store.GetModelAsync("",
            VoskModelStore.DefaultModelUrl, TextWriter.Null, CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(this.directory), Is.Empty);
    }

    [Test]
    public async Task IncompleteModelIsNotPublished()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("test-model/am/final.mdl");
        }

        using var handler = new ArchiveHandler(buffer.ToArray());
        using var client = new HttpClient(handler);
        var store = new VoskModelStore(client, this.directory);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetModelAsync("",
            VoskModelStore.DefaultModelUrl, TextWriter.Null, CancellationToken.None));
        Assert.That(Directory.GetFileSystemEntries(this.directory), Is.Empty);
    }

    private static byte[] CreateModelArchive(bool nested = true)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var path in new[] { "am/final.mdl", "conf/mfcc.conf", "conf/model.conf", "graph/HCLG.fst" })
            {
                archive.CreateEntry(nested ? $"test-model/{path}" : path);
            }
        }

        return buffer.ToArray();
    }

    private sealed class ArchiveHandler(byte[] archive, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public Action? OnRequest { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.RequestCount++;
            this.OnRequest?.Invoke();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(archive)
            });
        }
    }
}