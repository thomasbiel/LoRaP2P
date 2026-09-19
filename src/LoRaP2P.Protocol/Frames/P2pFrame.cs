using System.Text;

namespace LoRaP2P.Protocol.Frames;

public sealed class P2pFrame
{
    public const byte ProtocolVersion = 1;
    public const int HeaderLength = 12;
    public const int MaximumPayloadLength = 243;

    private readonly byte[] _payload;

    public P2pFrame(
        P2pMessageType type,
        P2pNodeId sender,
        P2pAddress recipient,
        uint sequenceNumber,
        ReadOnlySpan<byte> payload)
    {
        Validate(type, recipient, payload);
        Type = type;
        Sender = sender;
        Recipient = recipient;
        SequenceNumber = sequenceNumber;
        _payload = payload.ToArray();
    }

    public P2pMessageType Type { get; }

    public P2pNodeId Sender { get; }

    public P2pAddress Recipient { get; }

    public uint SequenceNumber { get; }

    public int PayloadLength => _payload.Length;

    public byte[] GetPayload() => (byte[])_payload.Clone();

    internal ReadOnlySpan<byte> PayloadSpan => _payload;

    public static P2pFrame CreateData(
        P2pNodeId sender,
        P2pAddress recipient,
        uint sequenceNumber,
        ReadOnlySpan<byte> payload) =>
        new(P2pMessageType.Data, sender, recipient, sequenceNumber, payload);

    public static P2pFrame CreateAck(P2pNodeId sender, P2pNodeId recipient, uint sequenceNumber) =>
        new(P2pMessageType.Ack, sender, new P2pAddress(recipient), sequenceNumber, []);

    public static P2pFrame CreateError(
        P2pNodeId sender,
        P2pNodeId recipient,
        uint sequenceNumber,
        P2pErrorCode errorCode,
        string? diagnostic = null)
    {
        if (!Enum.IsDefined(errorCode))
        {
            throw new ArgumentOutOfRangeException(nameof(errorCode));
        }

        var diagnosticBytes = diagnostic is null ? [] : Encoding.UTF8.GetBytes(diagnostic);
        if (diagnosticBytes.Length > MaximumPayloadLength - 1)
        {
            throw new ArgumentException("The UTF-8 diagnostic is too long.", nameof(diagnostic));
        }

        var payload = new byte[diagnosticBytes.Length + 1];
        payload[0] = (byte)errorCode;
        diagnosticBytes.CopyTo(payload, 1);
        return new P2pFrame(P2pMessageType.Error, sender, new P2pAddress(recipient), sequenceNumber, payload);
    }

    private static void Validate(P2pMessageType type, P2pAddress recipient, ReadOnlySpan<byte> payload)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        if (payload.Length > MaximumPayloadLength)
        {
            throw new ArgumentException($"Payload cannot exceed {MaximumPayloadLength} bytes.", nameof(payload));
        }

        switch (type)
        {
            case P2pMessageType.Data when payload.IsEmpty:
                throw new ArgumentException("A data frame requires a payload.", nameof(payload));
            case P2pMessageType.Ack when !payload.IsEmpty:
                throw new ArgumentException("An ACK frame cannot contain a payload.", nameof(payload));
            case P2pMessageType.Ack when recipient.IsBroadcast:
                throw new ArgumentException("An ACK frame cannot be addressed to broadcast.", nameof(recipient));
            case P2pMessageType.Error when payload.IsEmpty:
                throw new ArgumentException("An error frame requires an error code.", nameof(payload));
            case P2pMessageType.Error when recipient.IsBroadcast:
                throw new ArgumentException("An error frame cannot be addressed to broadcast.", nameof(recipient));
            case P2pMessageType.Error when payload[0] is < 1 or > 3:
                throw new ArgumentException("The error code is unknown.", nameof(payload));
        }
    }
}
