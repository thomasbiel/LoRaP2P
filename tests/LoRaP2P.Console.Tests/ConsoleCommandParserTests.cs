using LoRaP2P.Console.Commands;
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

    [SetUp]
    public void SetUp()
    {
        _radio = new FakeLoraRadio();
        _output = new StringWriter();
        _error = new StringWriter();
        _parser = new ConsoleCommandParser(_error);
        _context = new CommandContext(_radio, new RadioConfiguration(), _output);
    }

    [TearDown]
    public async Task TearDown()
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _parser.Dispose();
        await _radio.DisposeAsync();
        await _output.DisposeAsync();
        await _error.DisposeAsync();
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
    public async Task InteractiveConsole_RunAsync_DispatchesUntilQuit()
    {
        using StringReader input = new("probe\nquit\n");

        var exitCode = await InteractiveConsole.RunAsync(
            _radio,
            new RadioConfiguration(),
            input,
            _output,
            _error,
            CancellationToken.None);

        Assert.That(exitCode, Is.Zero);
        Assert.That(_output.ToString(), Does.Contain("SX1276 version: 0x12"));
        Assert.That(_error.ToString(), Is.Empty);
    }

    private Task<CommandOutcome> ExecuteAsync(string command) => _parser.ParseAndExecuteAsync(command, _context, CancellationToken.None);
}