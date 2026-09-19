using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Protocol.Tests;

internal sealed class ScriptedLoraRadio : ILoraRadio
{
    private readonly Queue<Func<TimeSpan, CancellationToken, Task<ReceivedPacket?>>> _receptions = [];

    public List<byte[]> Transmissions { get; } = [];

    public List<TimeSpan> ReceiveTimeouts { get; } = [];

    public Func<ReadOnlyMemory<byte>, CancellationToken, Task<TransmitResult>>? TransmitHandler { get; set; }

    public void EnqueuePacket(ReceivedPacket packet, Action? after = null) =>
        _receptions.Enqueue(
            (_, _) =>
            {
                after?.Invoke();
                return Task.FromResult<ReceivedPacket?>(packet);
            });

    public void EnqueueTimeout(Action? after = null) =>
        _receptions.Enqueue(
            (_, _) =>
            {
                after?.Invoke();
                return Task.FromResult<ReceivedPacket?>(null);
            });

    public Task<TransmitResult> TransmitAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        Transmissions.Add(payload.ToArray());
        return TransmitHandler?.Invoke(payload, cancellationToken) ??
               Task.FromResult(new TransmitResult(TimeSpan.FromMilliseconds(12), DateTimeOffset.UnixEpoch));
    }

    public Task<ReceivedPacket?> ReceiveSingleAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ReceiveTimeouts.Add(timeout);
        if (_receptions.TryDequeue(out var reception))
        {
            return reception(timeout, cancellationToken);
        }

        return WaitForCancellationAsync(cancellationToken);
    }

    public Task ResetAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<byte> ProbeAsync(CancellationToken cancellationToken = default) => Task.FromResult((byte)0x12);

    public Task ConfigureAsync(RadioConfiguration configuration, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<RadioStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<byte, byte>> ReadRegistersAsync(
        byte startAddress,
        int count,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<ReceivedPacket?> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return null;
    }
}

internal sealed class StubRandom(uint nextUInt32 = 0, int nextInt32 = 0) : Reliability.IP2pRandom
{
    public uint NextUInt32() => nextUInt32;

    public int NextInt32(int minimumInclusive, int maximumExclusive) =>
        Math.Clamp(nextInt32, minimumInclusive, maximumExclusive - 1);
}

internal sealed class RecordingDelay : IP2pDelay
{
    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(delay);
        return Task.CompletedTask;
    }
}

internal sealed class CancelingDelay(CancellationTokenSource cancellation) : IP2pDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        cancellation.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
