using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol.Reliability;

public sealed class DuplicateTracker
{
    private readonly Lock _lock = new();
    private readonly TimeSpan _window;
    private readonly int _entriesPerPeer;
    private readonly Dictionary<P2pNodeId, Queue<Entry>> _entries = [];

    public DuplicateTracker(TimeSpan window, int entriesPerPeer)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entriesPerPeer);

        _window = window;
        _entriesPerPeer = entriesPerPeer;
    }

    public bool IsDuplicateAndTrack(P2pNodeId sender, uint sequenceNumber, DateTimeOffset receivedAt)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(sender, out var peerEntries))
            {
                peerEntries = new Queue<Entry>();
                _entries.Add(sender, peerEntries);
            }

            var oldestAllowed = receivedAt - _window;
            while (peerEntries.TryPeek(out var oldest) && oldest.ReceivedAt < oldestAllowed)
            {
                peerEntries.Dequeue();
            }

            if (peerEntries.Any(entry => entry.SequenceNumber == sequenceNumber))
            {
                return true;
            }

            while (peerEntries.Count >= _entriesPerPeer)
            {
                peerEntries.Dequeue();
            }

            peerEntries.Enqueue(new Entry(sequenceNumber, receivedAt));
            return false;
        }
    }

    private readonly record struct Entry(uint SequenceNumber, DateTimeOffset ReceivedAt);
}
