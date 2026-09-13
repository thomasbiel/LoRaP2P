using System.Text;
using LoRaP2P.ConsoleApp;
using LoRaP2P.ConsoleApp.Commands;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Tests;

[TestFixture]
internal sealed class ConsoleCommandParserTests
{
    [Test]
    public async Task ParseAndExecuteAsync_SendText_PreservesQuotedPayload()
    {
        FakeLoraRadio radio = new();
        using StringWriter output = new();
        using StringWriter error = new();
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, new RadioConfiguration(), output);

        CommandOutcome outcome = await parser.ParseAndExecuteAsync(
            "send text \"hello world\"",
            context,
            CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(radio.TransmittedPayload, Is.EqualTo(Encoding.UTF8.GetBytes("hello world")));
        Assert.That(output.ToString(), Does.Contain("TxDone: 11 bytes"));
        Assert.That(error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_ConfigureThenReset_ReusesUpdatedConfiguration()
    {
        FakeLoraRadio radio = new();
        using StringWriter output = new();
        using StringWriter error = new();
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, new RadioConfiguration(), output);

        await parser.ParseAndExecuteAsync("configure frequency 868.3", context, CancellationToken.None);
        await parser.ParseAndExecuteAsync("reset", context, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(context.Configuration.FrequencyHertz, Is.EqualTo(868_300_000));
            Assert.That(radio.Configuration, Is.EqualTo(context.Configuration));
            Assert.That(radio.ResetCount, Is.EqualTo(1));
            Assert.That(error.ToString(), Is.Empty);
        });
    }

    [Test]
    public async Task ParseAndExecuteAsync_Receive_UsesProvidedTimeout()
    {
        FakeLoraRadio radio = new();
        using StringWriter output = new();
        using StringWriter error = new();
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, new RadioConfiguration(), output);

        await parser.ParseAndExecuteAsync("receive 2.5", context, CancellationToken.None);

        Assert.That(radio.ReceiveTimeout, Is.EqualTo(TimeSpan.FromSeconds(2.5)));
        Assert.That(output.ToString(), Does.Contain("Receive timeout."));
        Assert.That(error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_RegisterRead_UsesHexAddressAndCount()
    {
        FakeLoraRadio radio = new();
        using StringWriter output = new();
        using StringWriter error = new();
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, new RadioConfiguration(), output);

        await parser.ParseAndExecuteAsync("register read 0x42 3", context, CancellationToken.None);

        Assert.That(radio.RegisterRead, Is.EqualTo(((byte)0x42, 3)));
        Assert.That(output.ToString(), Does.Contain("0x42: 0x12"));
        Assert.That(error.ToString(), Is.Empty);
    }

    [TestCase("probe", "SX1276 version: 0x12")]
    [TestCase("status", "mode: Standby")]
    [TestCase("help", "configure frequency <MHz>")]
    [TestCase("register dump", "0x01: 0x12")]
    [TestCase("send hex DE AD BE EF", "TxDone: 4 bytes")]
    public async Task ParseAndExecuteAsync_RemainingVerbs_Execute(string command, string expectedOutput)
    {
        FakeLoraRadio radio = new();
        using StringWriter output = new();
        using StringWriter error = new();
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, new RadioConfiguration(), output);

        CommandOutcome outcome = await parser.ParseAndExecuteAsync(command, context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(output.ToString(), Does.Contain(expectedOutput));
        Assert.That(error.ToString(), Is.Empty);
    }

    [TestCase("quit")]
    [TestCase("exit")]
    public async Task ParseAndExecuteAsync_ExitVerbs_RequestExit(string command)
    {
        FakeLoraRadio radio = new();
        using StringWriter output = new();
        using StringWriter error = new();
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, new RadioConfiguration(), output);

        CommandOutcome outcome = await parser.ParseAndExecuteAsync(command, context, CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Exit));
        Assert.That(error.ToString(), Is.Empty);
    }

    [Test]
    public async Task ParseAndExecuteAsync_InvalidCommand_WritesParserErrorAndContinues()
    {
        FakeLoraRadio radio = new();
        using StringWriter output = new();
        using StringWriter error = new();
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, new RadioConfiguration(), output);

        CommandOutcome outcome = await parser.ParseAndExecuteAsync(
            "not-a-command",
            context,
            CancellationToken.None);

        Assert.That(outcome, Is.EqualTo(CommandOutcome.Continue));
        Assert.That(error.ToString(), Is.Not.Empty);
    }

    [Test]
    public async Task InteractiveConsole_RunAsync_DispatchesUntilQuit()
    {
        FakeLoraRadio radio = new();
        using StringReader input = new("probe\nquit\n");
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = await InteractiveConsole.RunAsync(
            radio,
            new RadioConfiguration(),
            input,
            output,
            error,
            CancellationToken.None);

        Assert.That(exitCode, Is.Zero);
        Assert.That(output.ToString(), Does.Contain("SX1276 version: 0x12"));
        Assert.That(error.ToString(), Is.Empty);
    }
}