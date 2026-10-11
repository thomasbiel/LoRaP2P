using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol.Reliability;

public sealed class DuplicateTracker
{
    private readonly Lock syncLock = new();
    private readonly TimeSpan window;
    private readonly int entriesPerPeer;
    private readonly Dictionary<P2pNodeId, Queue<Entry>> entries = [];

    public DuplicateTracker(TimeSpan window, int entriesPerPeer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entriesPerPeer);

        this.window = window;
        this.entriesPerPeer = entriesPerPeer;
    }

    public bool IsDuplicateAndTrack(P2pNodeId sender, uint sequenceNumber, DateTimeOffset receivedAt)
    {
        lock (this.syncLock)
        {
            if (!this.entries.TryGetValue(sender, out var peerEntries))
            {
                peerEntries = new Queue<Entry>();
                this.entries.Add(sender, peerEntries);
            }

            var oldestAllowed = receivedAt - this.window;
            while (peerEntries.TryPeek(out var oldest) && oldest.ReceivedAt < oldestAllowed)
            {
                peerEntries.Dequeue();
            }

            if (peerEntries.Any(entry => entry.SequenceNumber == sequenceNumber))
            {
                return true;
            }

            while (peerEntries.Count >= this.entriesPerPeer)
            {
                peerEntries.Dequeue();
            }

            peerEntries.Enqueue(new Entry(sequenceNumber, receivedAt));
            return false;
        }
    }

    private readonly record struct Entry(uint SequenceNumber, DateTimeOffset ReceivedAt);
}
