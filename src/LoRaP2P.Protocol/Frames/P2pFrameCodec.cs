using System.Buffers.Binary;

namespace LoRaP2P.Protocol.Frames;

public static class P2pFrameCodec
{
    public static byte[] Encode(P2pFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var encoded = new byte[P2pFrame.HeaderLength + frame.PayloadLength];
        encoded[0] = P2pFrame.ProtocolVersion;
        encoded[1] = (byte)frame.Type;
        encoded[2] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(3, 2), frame.Sender.Value);
        BinaryPrimitives.WriteUInt16BigEndian(encoded.AsSpan(5, 2), frame.Recipient.Value);
        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(7, 4), frame.SequenceNumber);
        encoded[11] = checked((byte)frame.PayloadLength);
        frame.PayloadSpan.CopyTo(encoded.AsSpan(P2pFrame.HeaderLength));
        return encoded;
    }

    public static P2pFrame Decode(ReadOnlySpan<byte> encoded)
    {
        if (encoded.Length < P2pFrame.HeaderLength)
        {
            throw new P2pProtocolException("The frame is shorter than the 12-byte header.");
        }

        if (encoded[0] != P2pFrame.ProtocolVersion)
        {
            throw new P2pProtocolException($"Unsupported protocol version {encoded[0]}.");
        }

        if (encoded[2] != 0)
        {
            throw new P2pProtocolException("Reserved flags must be zero.");
        }

        var type = (P2pMessageType)encoded[1];
        if (!Enum.IsDefined(type))
        {
            throw new P2pProtocolException($"Unknown message type {encoded[1]}.");
        }

        var senderValue = BinaryPrimitives.ReadUInt16BigEndian(encoded.Slice(3, 2));
        if (senderValue is 0 or ushort.MaxValue)
        {
            throw new P2pProtocolException("The sender ID is invalid.");
        }

        var recipientValue = BinaryPrimitives.ReadUInt16BigEndian(encoded.Slice(5, 2));
        if (recipientValue == 0)
        {
            throw new P2pProtocolException("The recipient ID is invalid.");
        }

        var payloadLength = encoded[11];
        if (encoded.Length != P2pFrame.HeaderLength + payloadLength)
        {
            throw new P2pProtocolException("The declared payload length does not match the frame length.");
        }

        try
        {
            return new P2pFrame(
                type,
                new P2pNodeId(senderValue),
                new P2pAddress(recipientValue),
                BinaryPrimitives.ReadUInt32BigEndian(encoded.Slice(7, 4)),
                encoded[P2pFrame.HeaderLength..]);
        }
        catch (ArgumentException exception)
        {
            throw new P2pProtocolException(exception.Message);
        }
    }
}
