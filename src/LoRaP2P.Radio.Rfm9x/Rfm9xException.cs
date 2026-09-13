namespace LoRaP2P.Radio.Rfm9x;

public sealed class Rfm9xException : Exception
{
    public Rfm9xException(string message) : base(message) { }

    public Rfm9xException(string message, Exception innerException) : base(message, innerException) { }
}