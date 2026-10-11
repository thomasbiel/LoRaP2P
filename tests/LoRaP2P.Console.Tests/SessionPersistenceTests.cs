using LoRaP2P.Protocol;
using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Console.Tests;

[TestFixture]
internal sealed class SessionPersistenceTests
{
    private string directory = null!;

    [SetUp]
    public void SetUp()
    {
        this.directory = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{Guid.NewGuid():N}-sessions");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }
    }

    [Test]
    public void SessionLog_AppendsSentAndReceivedMessagesToPeerFile()
    {
        using (var log = new P2pSessionLog(this.directory))
        {
            log.AppendSent(
                new P2pNodeId(1),
                new P2pNodeId(2),
                10,
                "outgoing"u8,
                "text",
                "acknowledged",
                attempts: 1);
            log.AppendReceived(
                new P2pNodeId(1),
                new P2pReceivedMessage(
                    new P2pNodeId(2),
                    new P2pAddress(new P2pNodeId(1)),
                    11,
                    "incoming"u8.ToArray(),
                    -70,
                    4.5,
                    DateTimeOffset.UtcNow,
                    IsDuplicate: false));
        }

        var lines = File.ReadAllLines(Directory.GetFiles(this.directory, "*.jsonl").Single());
        Assert.Multiple(() =>
        {
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Does.Contain("\"direction\":\"outgoing\""));
            Assert.That(lines[0], Does.Contain("\"text\":\"outgoing\""));
            Assert.That(lines[1], Does.Contain("\"direction\":\"incoming\""));
            Assert.That(lines[1], Does.Contain("\"rssiDbm\":-70"));
        });
    }

    [Test]
    public async Task ChatExporter_EncodesMessageTextAndCreatesSelfContainedHtml()
    {
        string sessionPath;
        using (var log = new P2pSessionLog(this.directory))
        {
            log.AppendSent(
                new P2pNodeId(1),
                new P2pNodeId(2),
                12,
                "<script>alert(1)</script>"u8,
                "text",
                "failed",
                attempts: 4);
            sessionPath = Directory.GetFiles(this.directory, "*.jsonl").Single();
        }

        var htmlPath = await P2pChatExporter.ExportAsync(
            sessionPath,
            outputPath: null,
            CancellationToken.None);
        var html = await File.ReadAllTextAsync(htmlPath);

        Assert.Multiple(() =>
        {
            Assert.That(htmlPath, Is.EqualTo(Path.ChangeExtension(sessionPath, ".html")));
            Assert.That(html, Does.Contain("&lt;script&gt;alert(1)&lt;/script&gt;"));
            Assert.That(html, Does.Not.Contain("<script>alert(1)</script>"));
            Assert.That(html, Does.Contain("class=\"row outgoing failed\""));
            Assert.That(html, Does.Contain("lang=\"en\""));
            Assert.That(html, Does.Contain(">Me<"));
            Assert.That(html, Does.Contain("4 attempt(s)"));
            Assert.That(html, Does.Contain("<style>"));
            Assert.That(html, Does.Not.Contain("{{"));
        });
    }

    [Test]
    public void ChatExporter_RejectsMalformedJsonLine()
    {
        Directory.CreateDirectory(this.directory);
        var sessionPath = Path.Combine(this.directory, "invalid.jsonl");
        File.WriteAllText(sessionPath, "{}");

        Assert.That(
            async () => await P2pChatExporter.ExportAsync(
                sessionPath,
                outputPath: null,
                CancellationToken.None),
            Throws.TypeOf<InvalidDataException>()
                .With.Message.Contains("line 1"));
    }
}
