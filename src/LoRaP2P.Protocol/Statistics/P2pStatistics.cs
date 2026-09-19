namespace LoRaP2P.Protocol.Statistics;

public sealed record P2pStatisticsSnapshot(
    long DataFramesSent,
    long DataFramesReceived,
    long AcksSent,
    long AcksReceived,
    long Retries,
    long AckTimeouts,
    long Duplicates,
    long CrcErrors,
    long InvalidFramesDropped,
    long ForeignFramesDropped,
    long DeliveryFailures);

public sealed class P2pStatistics
{
    private long _dataFramesSent;
    private long _dataFramesReceived;
    private long _acksSent;
    private long _acksReceived;
    private long _retries;
    private long _ackTimeouts;
    private long _duplicates;
    private long _crcErrors;
    private long _invalidFramesDropped;
    private long _foreignFramesDropped;
    private long _deliveryFailures;

    public P2pStatisticsSnapshot GetSnapshot() =>
        new(
            Interlocked.Read(ref _dataFramesSent),
            Interlocked.Read(ref _dataFramesReceived),
            Interlocked.Read(ref _acksSent),
            Interlocked.Read(ref _acksReceived),
            Interlocked.Read(ref _retries),
            Interlocked.Read(ref _ackTimeouts),
            Interlocked.Read(ref _duplicates),
            Interlocked.Read(ref _crcErrors),
            Interlocked.Read(ref _invalidFramesDropped),
            Interlocked.Read(ref _foreignFramesDropped),
            Interlocked.Read(ref _deliveryFailures));

    internal void DataFrameSent() => Interlocked.Increment(ref _dataFramesSent);

    internal void DataFrameReceived() => Interlocked.Increment(ref _dataFramesReceived);

    internal void AckSent() => Interlocked.Increment(ref _acksSent);

    internal void AckReceived() => Interlocked.Increment(ref _acksReceived);

    internal void Retry() => Interlocked.Increment(ref _retries);

    internal void AckTimeout() => Interlocked.Increment(ref _ackTimeouts);

    internal void Duplicate() => Interlocked.Increment(ref _duplicates);

    internal void CrcError() => Interlocked.Increment(ref _crcErrors);

    internal void InvalidFrameDropped() => Interlocked.Increment(ref _invalidFramesDropped);

    internal void ForeignFrameDropped() => Interlocked.Increment(ref _foreignFramesDropped);

    internal void DeliveryFailure() => Interlocked.Increment(ref _deliveryFailures);
}
