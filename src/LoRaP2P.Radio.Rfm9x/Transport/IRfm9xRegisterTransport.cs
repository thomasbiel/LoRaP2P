namespace LoRaP2P.Radio.Rfm9x.Transport;

public interface IRfm9xRegisterTransport : IDisposable
{
    byte ReadByte(byte address);

    void Read(byte address, Span<byte> destination);

    void WriteByte(byte address, byte value);

    void Write(byte address, ReadOnlySpan<byte> source);

    void SetReset(bool asserted);

    void ClearDio0Signal();

    ValueTask WaitForDio0RisingEdgeAsync(CancellationToken cancellationToken);
}