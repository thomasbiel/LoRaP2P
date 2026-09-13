namespace LoRaP2P.Radio.Rfm9x.Tests;

public sealed class Rfm9xFrequencyTests
{
    [Test]
    public void Eu868FrequencyRoundTripStaysWithinOneFrequencyStep()
    {
        const double frequencyHertz = 868_100_000;

        int registerValue = Rfm9xFrequency.ToRegisterValue(frequencyHertz);
        double actualFrequencyHertz = Rfm9xFrequency.FromRegisterValue(registerValue);

        Assert.That(
            Math.Abs(actualFrequencyHertz - frequencyHertz),
            Is.InRange(0, 32_000_000.0 / 524_288.0));
    }

    [Test]
    public void TimeOnAirMatchesKnownSf7Bw125Calculation()
    {
        var configuration = new RadioConfiguration();

        TimeSpan timeOnAir = Rfm9xTimeOnAir.Calculate(16, configuration);

        Assert.That(timeOnAir.TotalMilliseconds, Is.InRange(51.4, 51.5));
    }

    [Test]
    public void ConfigurationRejectsFrequencyOutsideEu868()
    {
        var configuration = new RadioConfiguration { FrequencyHertz = 915_000_000 };

        Assert.That(configuration.Validate, Throws.TypeOf<ArgumentOutOfRangeException>());
    }
}