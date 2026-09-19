using System.Security.Cryptography;

namespace LoRaP2P.Protocol.Reliability;

public interface IP2pRandom
{
    uint NextUInt32();

    int NextInt32(int minimumInclusive, int maximumExclusive);
}

public sealed class SystemP2pRandom : IP2pRandom
{
    public uint NextUInt32()
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt32(bytes);
    }

    public int NextInt32(int minimumInclusive, int maximumExclusive) =>
        RandomNumberGenerator.GetInt32(minimumInclusive, maximumExclusive);
}

public sealed class SequenceNumberGenerator
{
    private readonly Lock _lock = new();
    private uint _next;

    public SequenceNumberGenerator(IP2pRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        _next = random.NextUInt32();
    }

    public SequenceNumberGenerator(uint initialValue)
    {
        _next = initialValue;
    }

    public uint Next()
    {
        lock (_lock)
        {
            var value = _next;
            _next = unchecked(_next + 1);
            return value;
        }
    }
}
