namespace LoRaP2P.Radio.Rfm9x;

public static class Rfm9xTimeOnAir
{
    public static TimeSpan Calculate(int payloadLength, RadioConfiguration configuration)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(payloadLength);
        configuration.Validate();

        int spreadingFactor = (int)configuration.SpreadingFactor;
        double bandwidthHertz = (int)configuration.Bandwidth;
        double symbolDurationSeconds = Math.Pow(2, spreadingFactor) / bandwidthHertz;
        bool lowDataRateOptimize = symbolDurationSeconds > 0.016;

        double numerator = (8 * payloadLength)
            - (4 * spreadingFactor)
            + 28
            + (configuration.PayloadCrcEnabled ? 16 : 0)
            - (configuration.ImplicitHeader ? 20 : 0);
        double denominator = 4 * (spreadingFactor - (lowDataRateOptimize ? 2 : 0));
        double payloadSymbols = 8 + Math.Max(
            Math.Ceiling(numerator / denominator) * (int)configuration.CodingRate,
            0);

        double preambleSeconds = (configuration.PreambleSymbols + 4.25) * symbolDurationSeconds;
        double payloadSeconds = payloadSymbols * symbolDurationSeconds;
        return TimeSpan.FromSeconds(preambleSeconds + payloadSeconds);
    }
}