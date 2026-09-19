using CommandLine;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Tests;

[TestFixture]
internal sealed class RadioHardwareCheckTests
{
    [Test]
    public void StartupOptions_Check_Parses()
    {
        using Parser parser = new(settings => settings.HelpWriter = TextWriter.Null);

        var check = parser.ParseArguments<StartupOptions>(["--check"]).MapResult(
            options => options.Check,
            _ => false);

        Assert.That(check, Is.True);
    }

    [Test]
    public async Task RunAsync_ChecksRadioAndReportsSuccess()
    {
        FakeLoraRadio radio = new();
        var configuration = new RadioConfiguration();
        using CancellationTokenSource cancellation = new();
        TestRadioBonnetCheckUi bonnet = new(cancellation);
        await using StringWriter output = new();

        Assert.CatchAsync<OperationCanceledException>(
            async () => await RadioHardwareCheck.RunAsync(
                radio,
                configuration,
                bonnet,
                output,
                cancellation.Token));

        Assert.Multiple(() =>
        {
            Assert.That(radio.ResetCount, Is.EqualTo(1));
            Assert.That(radio.ProbeCount, Is.EqualTo(1));
            Assert.That(radio.StatusCount, Is.EqualTo(1));
            Assert.That(radio.Configuration, Is.EqualTo(configuration));
            Assert.That(bonnet.RadioDetected, Is.True);
            Assert.That(bonnet.DisplayedButtons, Is.EqualTo(RadioBonnetButtons.A));
            Assert.That(output.ToString(), Does.Contain("SPI/CE1 communication: OK"));
            Assert.That(output.ToString(), Does.Contain("RFM9x hardware check passed."));
        });
    }

    private sealed class TestRadioBonnetCheckUi(CancellationTokenSource cancellation) : IRadioBonnetCheckUi
    {
        public bool RadioDetected { get; private set; }

        public RadioBonnetButtons DisplayedButtons { get; private set; }

        public void ShowRadioStatus(bool detected)
        {
            RadioDetected = detected;
        }

        public RadioBonnetButtons ReadButtons()
        {
            cancellation.Cancel();
            return RadioBonnetButtons.A;
        }

        public void ShowButtons(RadioBonnetButtons buttons)
        {
            DisplayedButtons = buttons;
        }
    }
}
