using LoRaP2P.Console.Commands;
using LoRaP2P.Protocol;
using LoRaP2P.Protocol.Frames;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Tests;

[TestFixture]
internal sealed class ConsoleCommandParserTests : IAsyncDisposable
{
    private FakeLoraRadio radio = null!;
    private StringWriter output = null!;
    private StringWriter error = null!;
    private ConsoleCommandParser parser = null!;
    private CommandContext context = null!;
    private P2pConsoleSession p2p = null!;
    private TestRadioBonnetDisplay display = null!;
    private string p2pPath = null!;
    private string sessionsPath = null!;

    [SetUp]
    public void SetUp()
    {
        this.radio = new FakeLoraRadio();
        this.output = new StringWriter();
        this.error = new StringWriter();
        this.parser = new ConsoleCommandParser(this.error);
        this.p2pPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{Guid.NewGuid():N}-p2p.json");
        this.sessionsPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{Guid.NewGuid():N}-sessions");
        var p2pConfiguration = new P2pConfiguration
        {
            LocalNodeId = new P2pNodeId(1),
            Peers = [new P2pNodeId(2)]
        };
        this.display = new TestRadioBonnetDisplay();
        this.p2p = new P2pConsoleSession(
            this.radio,
            p2pConfiguration,
            new P2pConfigurationStore(this.p2pPath),
            new P2pSessionLog(this.sessionsPath),
            this.display);
        this.context = new CommandContext(this.radio, new RadioConfiguration(), this.p2p, this.output);
    }

    [TearDown]
    public async Task TearDown()
    {
        await this.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        this.parser.Dispose();
        this.p2p.Dispose();
        await this.radio.DisposeAsync();
        await this.output.DisposeAsync();
        await this.error.DisposeAsync();
        if (File.Exists(this.p2pPath))
        {
            File.Delete(this.p2pPath);
        }

        if (Directory.Exists(this.sessionsPath))
        {
            Directory.Delete(this.sessionsPath, recursive: true);
        }
    }

    [Test]
    public async Task ParseAndExecuteAsync_SendText_PreservesQuotedPayload()
    {
        var outcome = await this.ExecuteAsync("send text \"hello world\"");

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(this.radio.TransmittedPayload, Is.EqualTo([.. "hello world"u8]));
        Assert.That(this.output.ToString(), Does.Contain("TxDone: 11 bytes"));
        Assert.That(this.error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_ConfigureThenReset_ReusesUpdatedConfiguration()
    {
        await this.ExecuteAsync("configure frequency 868.3");
        await this.ExecuteAsync("reset");

        Assert.Multiple(() =>
        {
            Assert.That(this.context.Configuration.FrequencyHertz, Is.EqualTo(868_300_000));
            Assert.That(this.radio.Configuration, Is.EqualTo(this.context.Configuration));
            Assert.That(this.radio.ResetCount, Is.EqualTo(1));
            Assert.That(this.error.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_Receive_UsesProvidedTimeout()
    {
        await this.ExecuteAsync("receive 2.5");

        Assert.That(this.radio.ReceiveTimeout, Is.EqualTo(TimeSpan.FromSeconds(2.5)));
        Assert.That(this.output.ToString(), Does.Contain("Receive timeout."));
        Assert.That(this.error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_RegisterRead_UsesHexAddressAndCount()
    {
        await this.ExecuteAsync("register read 0x42 3");

        Assert.That(this.radio.RegisterRead, Is.EqualTo(((byte)0x42, 3)));
        Assert.That(this.output.ToString(), Does.Contain("0x42: 0x12"));
        Assert.That(this.error.ToString(), Is.Empty);
    }

    [TestCase("probe", "SX1276 version: 0x12")]
    [TestCase("status", "mode: Standby")]
    [TestCase("register dump", "0x01: 0x12")]
    [TestCase("send hex DE AD BE EF", "TxDone: 4 bytes")]
    public async Task ParseAndExecuteAsync_RemainingVerbs_Execute(string command, string expectedOutput)
    {
        var outcome = await this.ExecuteAsync(command);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(this.output.ToString(), Does.Contain(expectedOutput));
        Assert.That(this.error.ToString(), Is.Empty);
    }

    [TestCase("quit")]
    [TestCase("exit")]
    public async Task ParseAndExecuteAsync_ExitVerbs_RequestExit(string command)
    {
        var outcome = await this.ExecuteAsync(command);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Exit));
        Assert.That(this.error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_Help_WritesRegisteredVerbs()
    {
        var outcome = await this.ExecuteAsync("help");

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(this.error.ToString(), Does.Contain("configure"));
            Assert.That(this.error.ToString(), Does.Contain("send"));
            Assert.That(this.output.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_InvalidCommand_WritesParserErrorAndContinues()
    {
        var outcome = await this.ExecuteAsync("not-a-command");

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(this.error.ToString(), Is.Not.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_PeerCommands_PersistConfiguration()
    {
        await this.ExecuteAsync("peer id 3");
        await this.ExecuteAsync("peer add 4");

        var saved = await new P2pConfigurationStore(this.p2pPath).LoadAsync();
        Assert.Multiple(() =>
        {
            Assert.That(this.context.P2p.Node.LocalNodeId, Is.EqualTo(new P2pNodeId(3)));
            Assert.That(saved!.LocalNodeId, Is.EqualTo(new P2pNodeId(3)));
            Assert.That(saved.Peers, Does.Contain(new P2pNodeId(4)));
            Assert.That(this.output.ToString(), Does.Contain("Peer 4 registered."));
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_SendTo_WaitsForMatchingAck()
    {
        this.radio.AutoAcknowledgeP2p = true;

        var outcome = await this.ExecuteAsync("send-to 2 text \"hello peer\"");

        var frame = P2pFrameCodec.Decode(this.radio.TransmittedPayloads.Single());
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(frame.Recipient, Is.EqualTo(new P2pAddress(new P2pNodeId(2))));
            Assert.That(frame.GetPayload(), Is.EqualTo("hello peer"u8.ToArray()));
            Assert.That(this.output.ToString(), Does.Contain("ACK from peer 2"));
            Assert.That(this.output.ToString(), Does.Contain("not authenticated or encrypted"));
            Assert.That(Directory.GetFiles(this.sessionsPath, "*.jsonl"), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_Broadcast_SendsWithoutAck()
    {
        var outcome = await this.ExecuteAsync("broadcast text announcement");

        var frame = P2pFrameCodec.Decode(this.radio.TransmittedPayloads.Single());
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(frame.Recipient, Is.EqualTo(P2pAddress.Broadcast));
            Assert.That(this.output.ToString(), Does.Contain("no ACK expected"));
            Assert.That(
                Path.GetFileName(Directory.GetFiles(this.sessionsPath, "*.jsonl").Single()),
                Does.Contain("peerbroadcast"));
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_PeersAndStats_WriteSessionState()
    {
        await this.ExecuteAsync("peers");
        await this.ExecuteAsync("stats");

        Assert.Multiple(() =>
        {
            Assert.That(this.output.ToString(), Does.Contain("Local node: 1"));
            Assert.That(this.output.ToString(), Does.Contain("Peer 2: never received"));
            Assert.That(this.output.ToString(), Does.Contain("P2P stats:"));
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_Listen_WritesReceivedDataUntilCanceled()
    {
        using CancellationTokenSource cancellation = new();
        var frame = P2pFrame.CreateData(
            new P2pNodeId(2),
            new P2pAddress(new P2pNodeId(1)),
            7,
            "hello"u8);
        this.radio.QueueReceivedPacket(
            new ReceivedPacket(P2pFrameCodec.Encode(frame), true, -70, 4.5),
            cancellation.Cancel);

        await Assert.CatchAsync<OperationCanceledException>(
            async () => await this.parser.ParseAndExecuteAsync("listen", this.context, cancellation.Token));
        Assert.That(this.output.ToString(), Does.Contain("P2P Rx from 2: sequence 7"));
        Assert.That(
            ReadSessionText(Directory.GetFiles(this.sessionsPath, "*.jsonl").Single()),
            Does.Contain("\"direction\":\"incoming\""));
        Assert.That(this.display.Message, Is.Null);
    }

    [Test]
    public async Task ParseAndExecuteAsync_Listen_DisplaysBroadcastText()
    {
        using CancellationTokenSource cancellation = new();
        var frame = P2pFrame.CreateData(
            new P2pNodeId(2),
            P2pAddress.Broadcast,
            8,
            "hello display"u8);
        this.radio.QueueReceivedPacket(
            new ReceivedPacket(P2pFrameCodec.Encode(frame), true, -72, 3.5),
            cancellation.Cancel);

        await Assert.CatchAsync<OperationCanceledException>(
            async () => await this.parser.ParseAndExecuteAsync("listen", this.context, cancellation.Token));

        Assert.That(this.display.Message, Is.EqualTo("hello display"));
    }

    [Test]
    public async Task ParseAndExecuteAsync_ExportChat_WritesHtmlBesideSession()
    {
        this.radio.AutoAcknowledgeP2p = true;
        await this.ExecuteAsync("send-to 2 text \"hello peer\"");
        var sessionPath = Directory.GetFiles(this.sessionsPath, "*.jsonl").Single();

        var outcome = await this.ExecuteAsync($"export-chat \"{sessionPath}\"");

        var htmlPath = Path.ChangeExtension(sessionPath, ".html");
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(File.Exists(htmlPath), Is.True);
            Assert.That(File.ReadAllText(htmlPath), Does.Contain("hello peer"));
            Assert.That(this.output.ToString(), Does.Contain($"Chat exported to {htmlPath}"));
        });
    }

    [Test]
    public async Task InteractiveConsole_RunAsync_DispatchesUntilQuit()
    {
        using StringReader input = new("probe\nquit\n");

        var exitCode = await InteractiveConsole.RunAsync(
            this.radio,
            new RadioConfiguration(),
            this.p2p,
            input,
            this.output,
            this.error,
            CancellationToken.None);

        Assert.That(exitCode, Is.Zero);
        Assert.That(this.output.ToString(), Does.Contain("SX1276 version: 0x12"));
        Assert.That(this.error.ToString(), Is.Empty);
    }

    private Task<CommandOutcome> ExecuteAsync(string command) => this.parser.ParseAndExecuteAsync(command, this.context, CancellationToken.None);

    private static string ReadSessionText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class TestRadioBonnetDisplay : IRadioBonnetDisplay
    {
        public string? Message { get; private set; }

        public void ShowBroadcastMessage(string message)
        {
            this.Message = message;
        }
    }
}