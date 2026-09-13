namespace LoRaP2P.Radio.Rfm9x;

public static class Rfm9xTimeOnAir
{
    public static TimeSpan Calculate(int payloadLength, RadioConfiguration configuration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadLength);
        configuration.Validate();

        var spreadingFactor = (int)configuration.SpreadingFactor;
        double bandwidthHertz = (int)configuration.Bandwidth;
        var symbolDurationSeconds = Math.Pow(2, spreadingFactor) / bandwidthHertz;
        var lowDataRateOptimize = symbolDurationSeconds > 0.016;

        double numerator = (8 * payloadLength)
            - (4 * spreadingFactor)
            + 28
            + (configuration.PayloadCrcEnabled ? 16 : 0)
            - (configuration.ImplicitHeader ? 20 : 0);
        
        double denominator = 4 * (spreadingFactor - (lowDataRateOptimize ? 2 : 0));
        var payloadSymbols = 8 + Math.Max(
            Math.Ceiling(numerator / denominator) * (int)configuration.CodingRate,
            0);

        var preambleSeconds = (configuration.PreambleSymbols + 4.25) * symbolDurationSeconds;
        var payloadSeconds = payloadSymbols * symbolDurationSeconds;
        return TimeSpan.FromSeconds(preambleSeconds + payloadSeconds);
    }
}