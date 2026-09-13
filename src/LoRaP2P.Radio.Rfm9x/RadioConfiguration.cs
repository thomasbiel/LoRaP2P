namespace LoRaP2P.Radio.Rfm9x;

public sealed record RadioConfiguration
{
    public int SpiBusId { get; init; }

    public int ChipSelectLine { get; init; } = 1;

    public int ResetPin { get; init; } = 25;

    public int Dio0Pin { get; init; } = 22;

    public double FrequencyHertz { get; init; } = 868_100_000;

    public SpreadingFactor SpreadingFactor { get; init; } = SpreadingFactor.Sf7;

    public SignalBandwidth Bandwidth { get; init; } = SignalBandwidth.Khz125;

    public CodingRate CodingRate { get; init; } = CodingRate.FourOfFive;

    public int OutputPowerDbm { get; init; } = 14;

    public ushort PreambleSymbols { get; init; } = 8;

    public byte SyncWord { get; init; } = 0x34;

    public bool PayloadCrcEnabled { get; init; } = true;

    public bool ImplicitHeader { get; init; }

    public bool InvertIq { get; init; }

    public double DutyCycle { get; init; } = 0.01;

    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public void Validate()
    {
        if (SpiBusId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(SpiBusId));
        }

        if (ChipSelectLine < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ChipSelectLine));
        }

        if (ResetPin < 0 || Dio0Pin < 0 || ResetPin == Dio0Pin)
        {
            throw new ArgumentException("ResetPin and Dio0Pin must be distinct non-negative BCM GPIO numbers.");
        }

        if (FrequencyHertz is < 863_000_000 or > 870_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(FrequencyHertz), "EU868 frequency must be between 863 and 870 MHz.");
        }

        if (!Enum.IsDefined(SpreadingFactor))
        {
            throw new ArgumentOutOfRangeException(nameof(SpreadingFactor));
        }

        if (!Enum.IsDefined(Bandwidth))
        {
            throw new ArgumentOutOfRangeException(nameof(Bandwidth));
        }

        if (!Enum.IsDefined(CodingRate))
        {
            throw new ArgumentOutOfRangeException(nameof(CodingRate));
        }

        if (OutputPowerDbm is < 2 or > 14)
        {
            throw new ArgumentOutOfRangeException(nameof(OutputPowerDbm), "PoC output power is limited to 2 through 14 dBm for EU868.");
        }

        if (PreambleSymbols < 6)
        {
            throw new ArgumentOutOfRangeException(nameof(PreambleSymbols));
        }

        if (DutyCycle is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(DutyCycle));
        }

        if (OperationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(OperationTimeout));
        }
    }
}