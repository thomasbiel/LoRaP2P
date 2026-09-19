using System.Globalization;

namespace LoRaP2P.Protocol.Frames;

public readonly record struct P2pNodeId
{
    public const ushort MinimumValue = 1;
    public const ushort MaximumValue = 65_534;

    public P2pNodeId(ushort value)
    {
        if (value is < MinimumValue or > MaximumValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A node ID must be between 1 and 65534.");
        }

        Value = value;
    }

    public ushort Value { get; }

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
