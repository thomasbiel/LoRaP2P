namespace LoRaP2P.Radio.Rfm9x;

public sealed record RadioConfiguration
{
    public int SpiBusId { get; init; }

    public int ChipSelectLine { get; init; } = 1;

    public int ResetPin { get; init; } = 25;

    public int Dio0Pin { get; init; } = 22;

    public double FrequencyHertz { get; init; } = 869_525_000;

    public SpreadingFactor SpreadingFactor { get; init; } = SpreadingFactor.Sf7;

    public SignalBandwidth Bandwidth { get; init; } = SignalBandwidth.Khz125;

    public CodingRate CodingRate { get; init; } = CodingRate.FourOfFive;

    public int OutputPowerDbm { get; init; } = 14;

    public ushort PreambleSymbols { get; init; } = 8;

    public byte SyncWord { get; init; } = 0x34;

    public bool PayloadCrcEnabled { get; init; } = true;

    public bool ImplicitHeader { get; init; }

    public bool InvertIq { get; init; }

    public double DutyCycle { get; init; } = 0.1;

    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public void Validate()
    {
        if (this.SpiBusId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(this.SpiBusId));
        }

        if (this.ChipSelectLine < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(this.ChipSelectLine));
        }

        if (this.ResetPin < 0 || this.Dio0Pin < 0 || this.ResetPin == this.Dio0Pin)
        {
            throw new ArgumentException("ResetPin and Dio0Pin must be distinct non-negative BCM GPIO numbers.");
        }

        if (this.FrequencyHertz is < 863_000_000 or > 870_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(this.FrequencyHertz), "EU868 frequency must be between 863 and 870 MHz.");
        }

        if (!Enum.IsDefined(this.SpreadingFactor))
        {
            throw new ArgumentOutOfRangeException(nameof(this.SpreadingFactor));
        }

        if (!Enum.IsDefined(this.Bandwidth))
        {
            throw new ArgumentOutOfRangeException(nameof(this.Bandwidth));
        }

        if (!Enum.IsDefined(this.CodingRate))
        {
            throw new ArgumentOutOfRangeException(nameof(this.CodingRate));
        }

        if (this.OutputPowerDbm is < 2 or > 17)
        {
            throw new ArgumentOutOfRangeException(nameof(this.OutputPowerDbm), "PoC output power is limited to 2 through 17 dBm for EU868.");
        }

        if (this.PreambleSymbols < 6)
        {
            throw new ArgumentOutOfRangeException(nameof(this.PreambleSymbols));
        }

        if (this.DutyCycle is <= 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(this.DutyCycle));
        }

        if (this.OperationTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(this.OperationTimeout));
        }
    }
}