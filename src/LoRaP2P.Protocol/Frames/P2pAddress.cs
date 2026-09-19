using System.Globalization;

namespace LoRaP2P.Protocol.Frames;

public readonly record struct P2pAddress
{
    public const ushort BroadcastValue = ushort.MaxValue;

    public P2pAddress(ushort value)
    {
        if (value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "An address cannot be zero.");
        }

        Value = value;
    }

    public P2pAddress(P2pNodeId nodeId)
        : this(nodeId.Value)
    {
    }

    public static P2pAddress Broadcast { get; } = new(BroadcastValue);

    public ushort Value { get; }

    public bool IsBroadcast => Value == BroadcastValue;

    public P2pNodeId AsNodeId() =>
        IsBroadcast
            ? throw new InvalidOperationException("The broadcast address is not a node ID.")
            : new P2pNodeId(Value);

    public override string ToString() =>
        IsBroadcast ? "broadcast" : Value.ToString(CultureInfo.InvariantCulture);
}
