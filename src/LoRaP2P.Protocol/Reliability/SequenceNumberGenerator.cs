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
    private readonly Lock syncLock = new();
    private uint next;

    public SequenceNumberGenerator(IP2pRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        this.next = random.NextUInt32();
    }

    public SequenceNumberGenerator(uint initialValue)
    {
        this.next = initialValue;
    }

    public uint Next()
    {
        lock (this.syncLock)
        {
            var value = this.next;
            this.next = unchecked(this.next + 1);
            return value;
        }
    }
}
