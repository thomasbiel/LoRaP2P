namespace LoRaP2P.Radio.Rfm9x.Tests;

public sealed class Rfm9xFrequencyTests
{
    [Test]
    public void DefaultConfigurationUsesGermanBand54Profile()
    {
        var configuration = new RadioConfiguration();

        Assert.Multiple(() =>
        {
            Assert.That(configuration.FrequencyHertz, Is.EqualTo(869_525_000));
            Assert.That(configuration.Bandwidth, Is.EqualTo(SignalBandwidth.Khz125));
            Assert.That(configuration.OutputPowerDbm, Is.EqualTo(14));
            Assert.That(configuration.DutyCycle, Is.EqualTo(0.1));
        });
    }

    [Test]
    public void Eu868FrequencyRoundTripStaysWithinOneFrequencyStep()
    {
        const double frequencyHertz = 869_525_000;

        var registerValue = Rfm9xFrequency.ToRegisterValue(frequencyHertz);
        var actualFrequencyHertz = Rfm9xFrequency.FromRegisterValue(registerValue);

        Assert.That(
            Math.Abs(actualFrequencyHertz - frequencyHertz),
            Is.InRange(0, 32_000_000.0 / 524_288.0));
    }

    [Test]
    public void TimeOnAirMatchesKnownSf7Bw125Calculation()
    {
        var configuration = new RadioConfiguration();

        var timeOnAir = Rfm9xTimeOnAir.Calculate(16, configuration);

        Assert.That(timeOnAir.TotalMilliseconds, Is.InRange(51.4, 51.5));
    }

    [Test]
    public void ConfigurationRejectsFrequencyOutsideEu868()
    {
        var configuration = new RadioConfiguration { FrequencyHertz = 915_000_000 };

        Assert.That(configuration.Validate, Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void ConfigurationRejectsOutputPowerAboveSeventeenDbm()
    {
        var configuration = new RadioConfiguration { OutputPowerDbm = 18 };

        Assert.That(configuration.Validate, Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}