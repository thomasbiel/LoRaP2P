using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol.Peers;

public sealed record PeerSnapshot(
    P2pNodeId NodeId,
    DateTimeOffset? LastReceivedAt,
    int? LastRssiDbm,
    double? LastSnrDb,
    uint? LastSequenceNumber);

public sealed class PeerRegistry
{
    private readonly Lock syncLock = new();
    private readonly Dictionary<P2pNodeId, PeerState> peers;

    public PeerRegistry(IEnumerable<P2pNodeId>? configuredPeers = null)
    {
        this.peers = configuredPeers?.Distinct().ToDictionary(static id => id, static _ => new PeerState()) ?? [];
    }

    public bool Add(P2pNodeId peer)
    {
        lock (this.syncLock)
        {
            return this.peers.TryAdd(peer, new PeerState());
        }
    }

    public bool Contains(P2pNodeId peer)
    {
        lock (this.syncLock)
        {
            return this.peers.ContainsKey(peer);
        }
    }

    public void RecordReception(
        P2pNodeId peer,
        DateTimeOffset receivedAt,
        int rssiDbm,
        double snrDb,
        uint sequenceNumber)
    {
        lock (this.syncLock)
        {
            if (!this.peers.TryGetValue(peer, out var state))
            {
                state = new PeerState();
                this.peers.Add(peer, state);
            }

            state.LastReceivedAt = receivedAt;
            state.LastRssiDbm = rssiDbm;
            state.LastSnrDb = snrDb;
            state.LastSequenceNumber = sequenceNumber;
        }
    }

    public IReadOnlyList<PeerSnapshot> GetSnapshot()
    {
        lock (this.syncLock)
        {
            return [.. this.peers
                .OrderBy(static item => item.Key.Value)
                .Select(static item => new PeerSnapshot(
                    item.Key,
                    item.Value.LastReceivedAt,
                    item.Value.LastRssiDbm,
                    item.Value.LastSnrDb,
                    item.Value.LastSequenceNumber))];
        }
    }

    private sealed class PeerState
    {
        public DateTimeOffset? LastReceivedAt { get; set; }

        public int? LastRssiDbm { get; set; }

        public double? LastSnrDb { get; set; }

        public uint? LastSequenceNumber { get; set; }
    }
}
