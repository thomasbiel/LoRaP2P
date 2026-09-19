using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol;

public sealed record P2pConfiguration
{
    public required P2pNodeId LocalNodeId { get; init; }

    public IReadOnlyList<P2pNodeId> Peers { get; init; } = [];

    public P2pOptions Options { get; init; } = new();

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Peers);
        ArgumentNullException.ThrowIfNull(Options);
        Options.Validate();

        var uniquePeers = new HashSet<P2pNodeId>();
        foreach (var peer in Peers)
        {
            if (peer == LocalNodeId)
            {
                throw new ArgumentException("The local node ID cannot also be a peer.", nameof(Peers));
            }

            if (!uniquePeers.Add(peer))
            {
                throw new ArgumentException($"Peer {peer} occurs more than once.", nameof(Peers));
            }
        }
    }
}
