namespace LoRaP2P.Radio.Rfm9x;

public interface ILoraRadio : IAsyncDisposable
{
    Task ResetAsync(CancellationToken cancellationToken = default);

    Task<byte> ProbeAsync(CancellationToken cancellationToken = default);

    Task ConfigureAsync(RadioConfiguration configuration, CancellationToken cancellationToken = default);

    Task<TransmitResult> TransmitAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    Task<ReceivedPacket?> ReceiveSingleAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

    Task<RadioStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<byte, byte>> ReadRegistersAsync(byte startAddress, int count, CancellationToken cancellationToken = default);
}