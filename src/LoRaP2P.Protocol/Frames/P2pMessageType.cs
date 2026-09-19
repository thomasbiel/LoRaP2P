namespace LoRaP2P.Protocol.Frames;

public enum P2pMessageType : byte
{
    Data = 0x01,
    Ack = 0x02,
    Error = 0x03
}
