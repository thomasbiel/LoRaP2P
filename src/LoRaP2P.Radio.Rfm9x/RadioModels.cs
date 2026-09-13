namespace LoRaP2P.Radio.Rfm9x;

public enum SpreadingFactor
{
    Sf7 = 7,
    Sf8 = 8,
    Sf9 = 9,
    Sf10 = 10,
    Sf11 = 11,
    Sf12 = 12,
}

public enum SignalBandwidth
{
    Khz125 = 125_000,
    Khz250 = 250_000,
    Khz500 = 500_000,
}

public enum CodingRate
{
    FourOfFive = 5,
    FourOfSix = 6,
    FourOfSeven = 7,
    FourOfEight = 8,
}

public enum RadioMode
{
    Sleep,
    Standby,
    Transmit,
    ReceiveSingle,
}

public sealed record TransmitResult(TimeSpan TimeOnAir, DateTimeOffset CompletedAt);

public sealed record ReceivedPacket(byte[] Payload, bool IsCrcValid, int PacketRssiDbm, double PacketSnrDb);

public sealed record RadioStatus(byte ChipVersion, RadioMode Mode, byte IrqFlags, RadioConfiguration Configuration);