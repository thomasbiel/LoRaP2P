using LoRaP2P.Console.Commands;
using LoRaP2P.Protocol;
using LoRaP2P.Protocol.Frames;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Tests;

[TestFixture]
internal sealed class ConsoleCommandParserTests : IAsyncDisposable
{
    private FakeLoraRadio _radio = null!;
    private StringWriter _output = null!;
    private StringWriter _error = null!;
    private ConsoleCommandParser _parser = null!;
    private CommandContext _context = null!;
    private P2pConsoleSession _p2p = null!;
    private TestRadioBonnetDisplay _display = null!;
    private string _p2pPath = null!;
    private string _sessionsPath = null!;

    [SetUp]
    public void SetUp()
    {
        _radio = new FakeLoraRadio();
        _output = new StringWriter();
        _error = new StringWriter();
        _parser = new ConsoleCommandParser(_error);
        _p2pPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{Guid.NewGuid():N}-p2p.json");
        _sessionsPath = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            $"{Guid.NewGuid():N}-sessions");
        var p2pConfiguration = new P2pConfiguration
        {
            LocalNodeId = new P2pNodeId(1),
            Peers = [new P2pNodeId(2)]
        };
        _display = new TestRadioBonnetDisplay();
        _p2p = new P2pConsoleSession(
            _radio,
            p2pConfiguration,
            new P2pConfigurationStore(_p2pPath),
            new P2pSessionLog(_sessionsPath),
            _display);
        _context = new CommandContext(_radio, new RadioConfiguration(), _p2p, _output);
    }

    [TearDown]
    public async Task TearDown()
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _parser.Dispose();
        _p2p.Dispose();
        await _radio.DisposeAsync();
        await _output.DisposeAsync();
        await _error.DisposeAsync();
        if (File.Exists(_p2pPath))
        {
            File.Delete(_p2pPath);
        }

        if (Directory.Exists(_sessionsPath))
        {
            Directory.Delete(_sessionsPath, recursive: true);
        }
    }

    [Test]
    public async Task ParseAndExecuteAsync_SendText_PreservesQuotedPayload()
    {
        var outcome = await ExecuteAsync("send text \"hello world\"");

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(_radio.TransmittedPayload, Is.EqualTo([.. "hello world"u8]));
        Assert.That(_output.ToString(), Does.Contain("TxDone: 11 bytes"));
        Assert.That(_error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_ConfigureThenReset_ReusesUpdatedConfiguration()
    {
        await ExecuteAsync("configure frequency 868.3");
        await ExecuteAsync("reset");

        Assert.Multiple(() =>
        {
            Assert.That(_context.Configuration.FrequencyHertz, Is.EqualTo(868_300_000));
            Assert.That(_radio.Configuration, Is.EqualTo(_context.Configuration));
            Assert.That(_radio.ResetCount, Is.EqualTo(1));
            Assert.That(_error.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_Receive_UsesProvidedTimeout()
    {
        await ExecuteAsync("receive 2.5");

        Assert.That(_radio.ReceiveTimeout, Is.EqualTo(TimeSpan.FromSeconds(2.5)));
        Assert.That(_output.ToString(), Does.Contain("Receive timeout."));
        Assert.That(_error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_RegisterRead_UsesHexAddressAndCount()
    {
        await ExecuteAsync("register read 0x42 3");

        Assert.That(_radio.RegisterRead, Is.EqualTo(((byte)0x42, 3)));
        Assert.That(_output.ToString(), Does.Contain("0x42: 0x12"));
        Assert.That(_error.ToString(), Is.Empty);
    }

    [TestCase("probe", "SX1276 version: 0x12")]
    [TestCase("status", "mode: Standby")]
    [TestCase("register dump", "0x01: 0x12")]
    [TestCase("send hex DE AD BE EF", "TxDone: 4 bytes")]
    public async Task ParseAndExecuteAsync_RemainingVerbs_Execute(string command, string expectedOutput)
    {
        var outcome = await ExecuteAsync(command);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(_output.ToString(), Does.Contain(expectedOutput));
        Assert.That(_error.ToString(), Is.Empty);
    }

    [TestCase("quit")]
    [TestCase("exit")]
    public async Task ParseAndExecuteAsync_ExitVerbs_RequestExit(string command)
    {
        var outcome = await ExecuteAsync(command);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Exit));
        Assert.That(_error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_Help_WritesRegisteredVerbs()
    {
        var outcome = await ExecuteAsync("help");

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(_error.ToString(), Does.Contain("configure"));
            Assert.That(_error.ToString(), Does.Contain("send"));
            Assert.That(_output.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_InvalidCommand_WritesParserErrorAndContinues()
    {
        var outcome = await ExecuteAsync("not-a-command");

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(_error.ToString(), Is.Not.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_PeerCommands_PersistConfiguration()
    {
        await ExecuteAsync("peer id 3");
        await ExecuteAsync("peer add 4");

        var saved = await new P2pConfigurationStore(_p2pPath).LoadAsync();
        Assert.Multiple(() =>
        {
            Assert.That(_context.P2p.Node.LocalNodeId, Is.EqualTo(new P2pNodeId(3)));
            Assert.That(saved!.LocalNodeId, Is.EqualTo(new P2pNodeId(3)));
            Assert.That(saved.Peers, Does.Contain(new P2pNodeId(4)));
            Assert.That(_output.ToString(), Does.Contain("Peer 4 registered."));
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_SendTo_WaitsForMatchingAck()
    {
        _radio.AutoAcknowledgeP2p = true;

        var outcome = await ExecuteAsync("send-to 2 text \"hello peer\"");

        var frame = P2pFrameCodec.Decode(_radio.TransmittedPayloads.Single());
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(frame.Recipient, Is.EqualTo(new P2pAddress(new P2pNodeId(2))));
            Assert.That(frame.GetPayload(), Is.EqualTo("hello peer"u8.ToArray()));
            Assert.That(_output.ToString(), Does.Contain("ACK from peer 2"));
            Assert.That(_output.ToString(), Does.Contain("not authenticated or encrypted"));
            Assert.That(Directory.GetFiles(_sessionsPath, "*.jsonl"), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_Broadcast_SendsWithoutAck()
    {
        var outcome = await ExecuteAsync("broadcast text announcement");

        var frame = P2pFrameCodec.Decode(_radio.TransmittedPayloads.Single());
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(frame.Recipient, Is.EqualTo(P2pAddress.Broadcast));
            Assert.That(_output.ToString(), Does.Contain("no ACK expected"));
            Assert.That(
                Path.GetFileName(Directory.GetFiles(_sessionsPath, "*.jsonl").Single()),
                Does.Contain("peerbroadcast"));
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_PeersAndStats_WriteSessionState()
    {
        await ExecuteAsync("peers");
        await ExecuteAsync("stats");

        Assert.Multiple(() =>
        {
            Assert.That(_output.ToString(), Does.Contain("Local node: 1"));
            Assert.That(_output.ToString(), Does.Contain("Peer 2: never received"));
            Assert.That(_output.ToString(), Does.Contain("P2P stats:"));
        });
    }

    [Test]
    public void ParseAndExecuteAsync_Listen_WritesReceivedDataUntilCanceled()
    {
        using CancellationTokenSource cancellation = new();
        var frame = P2pFrame.CreateData(
            new P2pNodeId(2),
            new P2pAddress(new P2pNodeId(1)),
            7,
            "hello"u8);
        _radio.QueueReceivedPacket(
            new ReceivedPacket(P2pFrameCodec.Encode(frame), true, -70, 4.5),
            cancellation.Cancel);

        Assert.CatchAsync<OperationCanceledException>(
            async () => await _parser.ParseAndExecuteAsync("listen", _context, cancellation.Token));
        Assert.That(_output.ToString(), Does.Contain("P2P Rx from 2: sequence 7"));
        Assert.That(
            ReadSessionText(Directory.GetFiles(_sessionsPath, "*.jsonl").Single()),
            Does.Contain("\"direction\":\"incoming\""));
        Assert.That(_display.Message, Is.Null);
    }

    [Test]
    public void ParseAndExecuteAsync_Listen_DisplaysBroadcastText()
    {
        using CancellationTokenSource cancellation = new();
        var frame = P2pFrame.CreateData(
            new P2pNodeId(2),
            P2pAddress.Broadcast,
            8,
            "hello display"u8);
        _radio.QueueReceivedPacket(
            new ReceivedPacket(P2pFrameCodec.Encode(frame), true, -72, 3.5),
            cancellation.Cancel);

        Assert.CatchAsync<OperationCanceledException>(
            async () => await _parser.ParseAndExecuteAsync("listen", _context, cancellation.Token));

        Assert.That(_display.Message, Is.EqualTo("hello display"));
    }

    [Test]
    public async Task ParseAndExecuteAsync_ExportChat_WritesHtmlBesideSession()
    {
        _radio.AutoAcknowledgeP2p = true;
        await ExecuteAsync("send-to 2 text \"hello peer\"");
        var sessionPath = Directory.GetFiles(_sessionsPath, "*.jsonl").Single();

        var outcome = await ExecuteAsync($"export-chat \"{sessionPath}\"");

        var htmlPath = Path.ChangeExtension(sessionPath, ".html");
        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
            Assert.That(File.Exists(htmlPath), Is.True);
            Assert.That(File.ReadAllText(htmlPath), Does.Contain("hello peer"));
            Assert.That(_output.ToString(), Does.Contain($"Chat exported to {htmlPath}"));
        });
    }

    [Test]
    public async Task InteractiveConsole_RunAsync_DispatchesUntilQuit()
    {
        using StringReader input = new("probe\nquit\n");

        var exitCode = await InteractiveConsole.RunAsync(
            _radio,
            new RadioConfiguration(),
            _p2p,
            input,
            _output,
            _error,
            CancellationToken.None);

        Assert.That(exitCode, Is.Zero);
        Assert.That(_output.ToString(), Does.Contain("SX1276 version: 0x12"));
        Assert.That(_error.ToString(), Is.Empty);
    }

    private Task<CommandOutcome> ExecuteAsync(string command) => _parser.ParseAndExecuteAsync(command, _context, CancellationToken.None);

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
            Message = message;
        }
    }
}