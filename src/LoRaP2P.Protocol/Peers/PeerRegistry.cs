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
    private readonly Lock _lock = new();
    private readonly Dictionary<P2pNodeId, PeerState> _peers;

    public PeerRegistry(IEnumerable<P2pNodeId>? configuredPeers = null)
    {
        _peers = configuredPeers?.Distinct().ToDictionary(static id => id, static _ => new PeerState()) ?? [];
    }

    public bool Add(P2pNodeId peer)
    {
        lock (_lock)
        {
            return _peers.TryAdd(peer, new PeerState());
        }
    }

    public bool Contains(P2pNodeId peer)
    {
        lock (_lock)
        {
            return _peers.ContainsKey(peer);
        }
    }

    public void RecordReception(
        P2pNodeId peer,
        DateTimeOffset receivedAt,
        int rssiDbm,
        double snrDb,
        uint sequenceNumber)
    {
        lock (_lock)
        {
            if (!_peers.TryGetValue(peer, out var state))
            {
                state = new PeerState();
                _peers.Add(peer, state);
            }

            state.LastReceivedAt = receivedAt;
            state.LastRssiDbm = rssiDbm;
            state.LastSnrDb = snrDb;
            state.LastSequenceNumber = sequenceNumber;
        }
    }

    public IReadOnlyList<PeerSnapshot> GetSnapshot()
    {
        lock (_lock)
        {
            return _peers
                .OrderBy(static item => item.Key.Value)
                .Select(static item => new PeerSnapshot(
                    item.Key,
                    item.Value.LastReceivedAt,
                    item.Value.LastRssiDbm,
                    item.Value.LastSnrDb,
                    item.Value.LastSequenceNumber))
                .ToArray();
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
