namespace LoRaP2P.Protocol.Frames;

public sealed class P2pProtocolException : Exception
{
    public P2pProtocolException(string message)
        : base(message)
    {
    }
}
