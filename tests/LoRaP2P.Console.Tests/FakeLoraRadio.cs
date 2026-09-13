using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Tests;

internal sealed class FakeLoraRadio : ILoraRadio
{
    public RadioConfiguration Configuration { get; private set; } = new();

    public byte[]? TransmittedPayload { get; private set; }

    public TimeSpan? ReceiveTimeout { get; private set; }

    public (byte StartAddress, int Count)? RegisterRead { get; private set; }

    public int ResetCount { get; private set; }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        ResetCount++;
        return Task.CompletedTask;
    }

    public Task<byte> ProbeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult((byte)0x12);

    public Task ConfigureAsync(
        RadioConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        Configuration = configuration;
        return Task.CompletedTask;
    }

    public Task<TransmitResult> TransmitAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        TransmittedPayload = payload.ToArray();
        return Task.FromResult(new TransmitResult(TimeSpan.FromMilliseconds(10), DateTimeOffset.UtcNow));
    }

    public Task<ReceivedPacket?> ReceiveSingleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ReceiveTimeout = timeout;
        return Task.FromResult<ReceivedPacket?>(null);
    }

    public Task<RadioStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new RadioStatus(0x12, RadioMode.Standby, 0, Configuration));

    public Task<IReadOnlyDictionary<byte, byte>> ReadRegistersAsync(
        byte startAddress,
        int count,
        CancellationToken cancellationToken = default)
    {
        RegisterRead = (startAddress, count);
        IReadOnlyDictionary<byte, byte> registers = new Dictionary<byte, byte>
        {
            [startAddress] = 0x12,
        };
        return Task.FromResult(registers);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
