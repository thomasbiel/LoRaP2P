using LoRaP2P.Protocol.Frames;
using LoRaP2P.Protocol.Peers;
using LoRaP2P.Protocol.Reliability;
using LoRaP2P.Protocol.Statistics;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Protocol;

public sealed class P2pNode : IDisposable
{
    private readonly ILoraRadio _radio;
    private readonly P2pNodeId _localNodeId;
    private readonly P2pOptions _options;
    private readonly PeerRegistry _peers;
    private readonly P2pStatistics _statistics;
    private readonly SequenceNumberGenerator _sequences;
    private readonly DuplicateTracker _duplicates;
    private readonly TimeProvider _timeProvider;
    private readonly IP2pRandom _random;
    private readonly IP2pDelay _delay;
    private readonly SemaphoreSlim _transactionLock = new(1, 1);

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
        _radio = radio;
        _localNodeId = localNodeId;
        _options = options ?? new P2pOptions();
        _options.Validate();
        _peers = peers ?? new PeerRegistry();
        _statistics = statistics ?? new P2pStatistics();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _random = random ?? new SystemP2pRandom();
        _sequences = sequences ?? new SequenceNumberGenerator(_random);
        _duplicates = new DuplicateTracker(_options.DuplicateWindow, _options.DuplicateEntriesPerPeer);
        _delay = delay ?? new TimeProviderP2pDelay(_timeProvider);
    }

    public event Action<P2pReceivedMessage>? DataReceived;

    public P2pNodeId LocalNodeId => _localNodeId;

    public PeerRegistry Peers => _peers;

    public P2pStatistics Statistics => _statistics;

    public void Dispose() => _transactionLock.Dispose();

    public async Task<P2pSendResult> SendAsync(
        P2pNodeId peer,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var sequenceNumber = _sequences.Next();
        var frame = P2pFrame.CreateData(_localNodeId, new P2pAddress(peer), sequenceNumber, payload.Span);
        var encoded = P2pFrameCodec.Encode(frame);
        var attempts = _options.MaxRetries + 1;

        await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (attempt > 1)
                {
                    _statistics.Retry();
                }

                var transmitResult = await _radio.TransmitAsync(encoded, cancellationToken).ConfigureAwait(false);
                _statistics.DataFrameSent();
                var ackWaitStarted = _timeProvider.GetUtcNow();

                if (await WaitForAckAsync(peer, sequenceNumber, ackWaitStarted, cancellationToken).ConfigureAwait(false))
                {
                    return new P2pSendResult(
                        peer,
                        sequenceNumber,
                        attempt,
                        transmitResult.TimeOnAir,
                        _timeProvider.GetUtcNow() - ackWaitStarted);
                }

                _statistics.AckTimeout();
                if (attempt < attempts)
                {
                    await _delay.DelayAsync(GetBackoff(), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _transactionLock.Release();
        }

        _statistics.DeliveryFailure();
        throw new P2pDeliveryException(peer, sequenceNumber, attempts);
    }

    public async Task<P2pBroadcastResult> BroadcastAsync(
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var sequenceNumber = _sequences.Next();
        var frame = P2pFrame.CreateData(_localNodeId, P2pAddress.Broadcast, sequenceNumber, payload.Span);
        var encoded = P2pFrameCodec.Encode(frame);

        await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _radio.TransmitAsync(encoded, cancellationToken).ConfigureAwait(false);
            _statistics.DataFrameSent();
            return new P2pBroadcastResult(sequenceNumber, result.TimeOnAir, result.CompletedAt);
        }
        finally
        {
            _transactionLock.Release();
        }
    }

    public async Task ListenAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _transactionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var packet = await _radio
                    .ReceiveSingleAsync(_options.ListenPollTimeout, cancellationToken)
                    .ConfigureAwait(false);
                if (packet is not null)
                {
                    await ProcessPacketAsync(packet, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _transactionLock.Release();
            }
        }
    }

    private async Task<bool> WaitForAckAsync(
        P2pNodeId peer,
        uint sequenceNumber,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var deadline = startedAt + _options.AckTimeout;
        while (true)
        {
            var remaining = deadline - _timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return false;
            }

            var packet = await _radio.ReceiveSingleAsync(remaining, cancellationToken).ConfigureAwait(false);
            if (packet is null)
            {
                return false;
            }

            var frame = await ProcessPacketAsync(packet, cancellationToken).ConfigureAwait(false);
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
            _statistics.CrcError();
            return null;
        }

        P2pFrame frame;
        try
        {
            frame = P2pFrameCodec.Decode(packet.Payload);
        }
        catch (P2pProtocolException)
        {
            _statistics.InvalidFrameDropped();
            return null;
        }

        if ((frame.Recipient.Value != _localNodeId.Value && !frame.Recipient.IsBroadcast) ||
            frame.Sender == _localNodeId)
        {
            _statistics.ForeignFrameDropped();
            return null;
        }

        var receivedAt = _timeProvider.GetUtcNow();
        _peers.RecordReception(
            frame.Sender,
            receivedAt,
            packet.PacketRssiDbm,
            packet.PacketSnrDb,
            frame.SequenceNumber);

        switch (frame.Type)
        {
            case P2pMessageType.Data:
                await ProcessDataAsync(frame, packet, receivedAt, cancellationToken).ConfigureAwait(false);
                break;
            case P2pMessageType.Ack:
                _statistics.AckReceived();
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
        _statistics.DataFrameReceived();
        var isDuplicate = _duplicates.IsDuplicateAndTrack(frame.Sender, frame.SequenceNumber, receivedAt);
        if (isDuplicate)
        {
            _statistics.Duplicate();
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
            await _delay.DelayAsync(_options.AckTurnaroundDelay, cancellationToken).ConfigureAwait(false);
            var ack = P2pFrame.CreateAck(_localNodeId, frame.Sender, frame.SequenceNumber);
            await _radio.TransmitAsync(P2pFrameCodec.Encode(ack), cancellationToken).ConfigureAwait(false);
            _statistics.AckSent();
        }
    }

    private TimeSpan GetBackoff()
    {
        if (_options.BackoffMinimum == _options.BackoffMaximum)
        {
            return _options.BackoffMinimum;
        }

        var rangeTicks = _options.BackoffMaximum.Ticks - _options.BackoffMinimum.Ticks;
        var fraction = _random.NextInt32(0, int.MaxValue) / (double)(int.MaxValue - 1);
        return _options.BackoffMinimum + TimeSpan.FromTicks((long)(rangeTicks * fraction));
    }
}
