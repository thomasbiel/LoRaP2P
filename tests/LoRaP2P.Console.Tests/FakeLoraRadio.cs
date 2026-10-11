using LoRaP2P.Protocol.Frames;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Tests;

internal sealed class FakeLoraRadio : ILoraRadio
{
    private readonly Queue<(ReceivedPacket Packet, Action? After)> receivedPackets = [];

    public RadioConfiguration Configuration { get; private set; } = new();

    public byte[]? TransmittedPayload { get; private set; }

    public List<byte[]> TransmittedPayloads { get; } = [];

    public TimeSpan? ReceiveTimeout { get; private set; }

    public (byte StartAddress, int Count)? RegisterRead { get; private set; }

    public int ResetCount { get; private set; }

    public int ProbeCount { get; private set; }

    public int StatusCount { get; private set; }

    public bool AutoAcknowledgeP2p { get; set; }

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        this.ResetCount++;
        return Task.CompletedTask;
    }

    public Task<byte> ProbeAsync(CancellationToken cancellationToken = default)
    {
        this.ProbeCount++;
        return Task.FromResult((byte)0x12);
    }

    public Task ConfigureAsync(RadioConfiguration configuration, CancellationToken cancellationToken = default)
    {
        this.Configuration = configuration;
        return Task.CompletedTask;
    }

    public Task<TransmitResult> TransmitAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        this.TransmittedPayload = payload.ToArray();
        this.TransmittedPayloads.Add(this.TransmittedPayload);
        if (this.AutoAcknowledgeP2p)
        {
            var frame = P2pFrameCodec.Decode(payload.Span);
            if (frame.Type == P2pMessageType.Data && !frame.Recipient.IsBroadcast)
            {
                var ack = P2pFrame.CreateAck(
                    frame.Recipient.AsNodeId(),
                    frame.Sender,
                    frame.SequenceNumber);
                this.QueueReceivedPacket(
                    new ReceivedPacket(P2pFrameCodec.Encode(ack), true, -60, 5));
            }
        }

        return Task.FromResult(new TransmitResult(TimeSpan.FromMilliseconds(10), DateTimeOffset.UtcNow));
    }

    public Task<ReceivedPacket?> ReceiveSingleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        this.ReceiveTimeout = timeout;
        if (this.receivedPackets.TryDequeue(out var reception))
        {
            reception.After?.Invoke();
            return Task.FromResult<ReceivedPacket?>(reception.Packet);
        }

        return Task.FromResult<ReceivedPacket?>(null);
    }

    public void QueueReceivedPacket(ReceivedPacket packet, Action? after = null) =>
        this.receivedPackets.Enqueue((packet, after));

    public Task<RadioStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        this.StatusCount++;
        return Task.FromResult(new RadioStatus(0x12, RadioMode.Standby, 0, this.Configuration));
    }

    public Task<IReadOnlyDictionary<byte, byte>> ReadRegistersAsync(
        byte startAddress,
        int count,
        CancellationToken cancellationToken = default)
    {
        this.RegisterRead = (startAddress, count);
        return Task.FromResult<IReadOnlyDictionary<byte, byte>>(new Dictionary<byte, byte> { [startAddress] = 0x12 });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
