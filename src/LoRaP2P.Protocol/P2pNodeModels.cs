using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol;

public sealed record P2pReceivedMessage(
    P2pNodeId Sender,
    P2pAddress Recipient,
    uint SequenceNumber,
    byte[] Payload,
    int RssiDbm,
    double SnrDb,
    DateTimeOffset ReceivedAt,
    bool IsDuplicate);

public sealed record P2pSendResult(
    P2pNodeId Peer,
    uint SequenceNumber,
    int Attempts,
    TimeSpan LastTimeOnAir,
    TimeSpan AckLatency);

public sealed record P2pBroadcastResult(
    uint SequenceNumber,
    TimeSpan TimeOnAir,
    DateTimeOffset CompletedAt);

public sealed class P2pDeliveryException : Exception
{
    public P2pDeliveryException(P2pNodeId peer, uint sequenceNumber, int attempts)
        : base($"Peer {peer} did not acknowledge sequence {sequenceNumber} after {attempts} attempts.")
    {
        Peer = peer;
        SequenceNumber = sequenceNumber;
        Attempts = attempts;
    }

    public P2pNodeId Peer { get; }

    public uint SequenceNumber { get; }

    public int Attempts { get; }
}

public interface IP2pDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public sealed class TimeProviderP2pDelay : IP2pDelay
{
    private readonly TimeProvider _timeProvider;

    public TimeProviderP2pDelay(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, _timeProvider, cancellationToken);
}
