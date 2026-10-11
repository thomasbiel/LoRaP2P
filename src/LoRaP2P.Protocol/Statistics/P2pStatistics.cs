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
    private long dataFramesSent;
    private long dataFramesReceived;
    private long acksSent;
    private long acksReceived;
    private long retries;
    private long ackTimeouts;
    private long duplicates;
    private long crcErrors;
    private long invalidFramesDropped;
    private long foreignFramesDropped;
    private long deliveryFailures;

    public P2pStatisticsSnapshot GetSnapshot() =>
        new(
            Interlocked.Read(ref this.dataFramesSent),
            Interlocked.Read(ref this.dataFramesReceived),
            Interlocked.Read(ref this.acksSent),
            Interlocked.Read(ref this.acksReceived),
            Interlocked.Read(ref this.retries),
            Interlocked.Read(ref this.ackTimeouts),
            Interlocked.Read(ref this.duplicates),
            Interlocked.Read(ref this.crcErrors),
            Interlocked.Read(ref this.invalidFramesDropped),
            Interlocked.Read(ref this.foreignFramesDropped),
            Interlocked.Read(ref this.deliveryFailures));

    internal void DataFrameSent() => Interlocked.Increment(ref this.dataFramesSent);

    internal void DataFrameReceived() => Interlocked.Increment(ref this.dataFramesReceived);

    internal void AckSent() => Interlocked.Increment(ref this.acksSent);

    internal void AckReceived() => Interlocked.Increment(ref this.acksReceived);

    internal void Retry() => Interlocked.Increment(ref this.retries);

    internal void AckTimeout() => Interlocked.Increment(ref this.ackTimeouts);

    internal void Duplicate() => Interlocked.Increment(ref this.duplicates);

    internal void CrcError() => Interlocked.Increment(ref this.crcErrors);

    internal void InvalidFrameDropped() => Interlocked.Increment(ref this.invalidFramesDropped);

    internal void ForeignFrameDropped() => Interlocked.Increment(ref this.foreignFramesDropped);

    internal void DeliveryFailure() => Interlocked.Increment(ref this.deliveryFailures);
}
