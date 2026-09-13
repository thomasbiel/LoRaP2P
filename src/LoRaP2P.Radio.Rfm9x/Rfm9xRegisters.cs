namespace LoRaP2P.Radio.Rfm9x;

internal enum Rfm9xRegister : byte
{
    Fifo = 0x00,
    OpMode = 0x01,
    FrequencyMsb = 0x06,
    FrequencyMid = 0x07,
    FrequencyLsb = 0x08,
    PaConfig = 0x09,
    Lna = 0x0C,
    FifoAddressPointer = 0x0D,
    FifoTxBaseAddress = 0x0E,
    FifoRxBaseAddress = 0x0F,
    FifoRxCurrentAddress = 0x10,
    IrqFlagsMask = 0x11,
    IrqFlags = 0x12,
    RxByteCount = 0x13,
    PacketSnr = 0x19,
    PacketRssi = 0x1A,
    HopChannel = 0x1C,
    ModemConfig1 = 0x1D,
    ModemConfig2 = 0x1E,
    SymbolTimeoutLsb = 0x1F,
    PreambleMsb = 0x20,
    PreambleLsb = 0x21,
    PayloadLength = 0x22,
    MaxPayloadLength = 0x23,
    ModemConfig3 = 0x26,
    DetectOptimize = 0x31,
    InvertIq = 0x33,
    DetectionThreshold = 0x37,
    SyncWord = 0x39,
    InvertIq2 = 0x3B,
    DioMapping1 = 0x40,
    Version = 0x42,
}

internal static class Rfm9xBits
{
    public const byte LongRangeMode = 0x80;
    public const byte LowFrequencyMode = 0x08;
    public const byte ModeSleep = 0x00;
    public const byte ModeStandby = 0x01;
    public const byte ModeTransmit = 0x03;
    public const byte ModeReceiveSingle = 0x06;
    public const byte Dio0RxDone = 0x00;
    public const byte Dio0TxDone = 0x40;
    public const byte IrqRxTimeout = 0x80;
    public const byte IrqRxDone = 0x40;
    public const byte IrqPayloadCrcError = 0x20;
    public const byte IrqTxDone = 0x08;
    public const byte IrqClearAll = 0xFF;
    public const byte HopChannelPayloadCrc = 0x40;
    public const byte VersionExpected = 0x12;
}