using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol;

public sealed record P2pConfiguration
{
    public required P2pNodeId LocalNodeId { get; init; }

    public IReadOnlyList<P2pNodeId> Peers { get; init; } = [];

    public P2pOptions Options { get; init; } = new();

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(this.Peers);
        ArgumentNullException.ThrowIfNull(this.Options);
        this.Options.Validate();

        var uniquePeers = new HashSet<P2pNodeId>();
        foreach (var peer in this.Peers)
        {
            if (peer == this.LocalNodeId)
            {
                throw new ArgumentException("The local node ID cannot also be a peer.", nameof(this.Peers));
            }

            if (!uniquePeers.Add(peer))
            {
                throw new ArgumentException($"Peer {peer} occurs more than once.", nameof(this.Peers));
            }
        }
    }
}
