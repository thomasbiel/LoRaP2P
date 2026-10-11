using LoRaP2P.Protocol.Frames;
using LoRaP2P.Protocol.Peers;
using LoRaP2P.Protocol.Reliability;
using LoRaP2P.Protocol.Statistics;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Protocol;

public sealed class P2pNode : IDisposable
{
    private readonly ILoraRadio radio;
    private readonly P2pNodeId localNodeId;
    private readonly P2pOptions options;
    private readonly PeerRegistry peers;
    private readonly P2pStatistics statistics;
    private readonly SequenceNumberGenerator sequences;
    private readonly DuplicateTracker duplicates;
    private readonly TimeProvider timeProvider;
    private readonly IP2pRandom random;
    private readonly IP2pDelay delay;
    private readonly SemaphoreSlim transactionLock = new(1, 1);

    public P2pNode(
        ILoraRadio radio,
        P2pNodeId localNodeId,
        P2pOptions? options = null,
        PeerRegistry? peers = null,
        P2pStatistics? statistics = null,
        SequenceNumberGenerator? sequences = null,
        TimeProvider? timeProvider = null,
        IP2pRandom? random = null,
        IP2pDelay? delay = null)
    {
        ArgumentNullException.ThrowIfNull(radio);
        this.radio = radio;
        this.localNodeId = localNodeId;
        this.options = options ?? new P2pOptions();
        this.options.Validate();
        this.peers = peers ?? new PeerRegistry();
        this.statistics = statistics ?? new P2pStatistics();
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.random = random ?? new SystemP2pRandom();
        this.sequences = sequences ?? new SequenceNumberGenerator(this.random);
        this.duplicates = new DuplicateTracker(this.options.DuplicateWindow, this.options.DuplicateEntriesPerPeer);
        this.delay = delay ?? new TimeProviderP2pDelay(this.timeProvider);
    }

    public event Action<P2pReceivedMessage>? DataReceived;

    public P2pNodeId LocalNodeId => this.localNodeId;

    public PeerRegistry Peers => this.peers;

    public P2pStatistics Statistics => this.statistics;

    public void Dispose() => this.transactionLock.Dispose();

    public async Task<P2pSendResult> SendAsync(
        P2pNodeId peer,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var sequenceNumber = this.sequences.Next();
        var frame = P2pFrame.CreateData(this.localNodeId, new P2pAddress(peer), sequenceNumber, payload.Span);
        var encoded = P2pFrameCodec.Encode(frame);
        var attempts = this.options.MaxRetries + 1;

        await this.transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (attempt > 1)
                {
                    this.statistics.Retry();
                }

                var transmitResult = await this.radio.TransmitAsync(encoded, cancellationToken).ConfigureAwait(false);
                this.statistics.DataFrameSent();
                var ackWaitStarted = this.timeProvider.GetUtcNow();

                if (await this.WaitForAckAsync(peer, sequenceNumber, ackWaitStarted, cancellationToken).ConfigureAwait(false))
                {
                    return new P2pSendResult(
                        peer,
                        sequenceNumber,
                        attempt,
                        transmitResult.TimeOnAir,
                        this.timeProvider.GetUtcNow() - ackWaitStarted);
                }

                this.statistics.AckTimeout();
                if (attempt < attempts)
                {
                    await this.delay.DelayAsync(this.GetBackoff(), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            this.transactionLock.Release();
        }

        this.statistics.DeliveryFailure();
        throw new P2pDeliveryException(peer, sequenceNumber, attempts);
    }

    public async Task<P2pBroadcastResult> BroadcastAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var sequenceNumber = this.sequences.Next();
        var frame = P2pFrame.CreateData(this.localNodeId, P2pAddress.Broadcast, sequenceNumber, payload.Span);
        var encoded = P2pFrameCodec.Encode(frame);

        await this.transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await this.radio.TransmitAsync(encoded, cancellationToken).ConfigureAwait(false);
            this.statistics.DataFrameSent();
            return new P2pBroadcastResult(sequenceNumber, result.TimeOnAir, result.CompletedAt);
        }
        finally
        {
            this.transactionLock.Release();
        }
    }

    public async Task ListenAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var packet = await this.radio
                    .ReceiveSingleAsync(this.options.ListenPollTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (packet is not null)
                {
                    await this.ProcessPacketAsync(packet, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                this.transactionLock.Release();
            }
        }
    }

    private async Task<bool> WaitForAckAsync(
        P2pNodeId peer,
        uint sequenceNumber,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var deadline = startedAt + this.options.AckTimeout;
        while (true)
        {
            var remaining = deadline - this.timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            var packet = await this.radio.ReceiveSingleAsync(remaining, cancellationToken).ConfigureAwait(false);
            if (packet is null)
            {
                return false;
            }

            var frame = await this.ProcessPacketAsync(packet, cancellationToken).ConfigureAwait(false);
            if (frame is
                {
                    Type: P2pMessageType.Ack,
                    SequenceNumber: var receivedSequence,
                    Sender: var sender
                } &&
                sender == peer &&
                receivedSequence == sequenceNumber)
            {
                return true;
            }
        }
    }

    private async Task<P2pFrame?> ProcessPacketAsync(ReceivedPacket packet, CancellationToken cancellationToken)
    {
        if (!packet.IsCrcValid)
        {
            this.statistics.CrcError();
            return null;
        }

        P2pFrame frame;
        try
        {
            frame = P2pFrameCodec.Decode(packet.Payload);
        }
        catch (P2pProtocolException)
        {
            this.statistics.InvalidFrameDropped();
            return null;
        }

        if ((frame.Recipient.Value != this.localNodeId.Value && !frame.Recipient.IsBroadcast) ||
            frame.Sender == this.localNodeId)
        {
            this.statistics.ForeignFrameDropped();
            return null;
        }

        var receivedAt = this.timeProvider.GetUtcNow();
        this.peers.RecordReception(
            frame.Sender,
            receivedAt,
            packet.PacketRssiDbm,
            packet.PacketSnrDb,
            frame.SequenceNumber);

        switch (frame.Type)
        {
            case P2pMessageType.Data:
                await this.ProcessDataAsync(frame, packet, receivedAt, cancellationToken).ConfigureAwait(false);
                break;
            case P2pMessageType.Ack:
                this.statistics.AckReceived();
                break;
            case P2pMessageType.Error:
                break;
        }

        return frame;
    }

    private async Task ProcessDataAsync(
        P2pFrame frame,
        ReceivedPacket packet,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken)
    {
        this.statistics.DataFrameReceived();
        var isDuplicate = this.duplicates.IsDuplicateAndTrack(frame.Sender, frame.SequenceNumber, receivedAt);
        if (isDuplicate)
        {
            this.statistics.Duplicate();
        }
        else
        {
            DataReceived?.Invoke(
                new P2pReceivedMessage(
                    frame.Sender,
                    frame.Recipient,
                    frame.SequenceNumber,
                    frame.GetPayload(),
                    packet.PacketRssiDbm,
                    packet.PacketSnrDb,
                    receivedAt,
                    IsDuplicate: false));
        }

        if (!frame.Recipient.IsBroadcast)
        {
            await this.delay.DelayAsync(this.options.AckTurnaroundDelay, cancellationToken).ConfigureAwait(false);
            var ack = P2pFrame.CreateAck(this.localNodeId, frame.Sender, frame.SequenceNumber);
            await this.radio.TransmitAsync(P2pFrameCodec.Encode(ack), cancellationToken).ConfigureAwait(false);
            this.statistics.AckSent();
        }
    }

    private TimeSpan GetBackoff()
    {
        if (this.options.BackoffMinimum == this.options.BackoffMaximum)
        {
            return this.options.BackoffMinimum;
        }

        var rangeTicks = this.options.BackoffMaximum.Ticks - this.options.BackoffMinimum.Ticks;
        var fraction = this.random.NextInt32(0, int.MaxValue) / (double)(int.MaxValue - 1);
        return this.options.BackoffMinimum + TimeSpan.FromTicks((long)(rangeTicks * fraction));
    }
}
