using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol.Tests;

public sealed class FrameCodecTests
{
    private static readonly P2pNodeId Sender = new(0x1234);
    private static readonly P2pNodeId Recipient = new(0x5678);

    [Test]
    public void DataRoundTripsAtMinimumAndMaximumPayloadLengths()
    {
        foreach (var payload in new[] { new byte[] { 0x42 }, new byte[P2pFrame.MaximumPayloadLength] })
        {
            var decoded = RoundTrip(P2pFrame.CreateData(Sender, new P2pAddress(Recipient), 0x89ABCDEF, payload));

            Assert.Multiple(() =>
            {
                Assert.That(decoded.Type, Is.EqualTo(P2pMessageType.Data));
                Assert.That(decoded.Sender, Is.EqualTo(Sender));
                Assert.That(decoded.Recipient, Is.EqualTo(new P2pAddress(Recipient)));
                Assert.That(decoded.SequenceNumber, Is.EqualTo(0x89ABCDEF));
                Assert.That(decoded.GetPayload(), Is.EqualTo(payload));
            });
        }
    }

    [Test]
    public void AckAndErrorRoundTrip()
    {
        var ack = RoundTrip(P2pFrame.CreateAck(Sender, Recipient, 7));
        var error = RoundTrip(P2pFrame.CreateError(Sender, Recipient, 8, P2pErrorCode.ReceiverBusy, "busy"));

        Assert.Multiple(() =>
        {
            Assert.That(ack.Type, Is.EqualTo(P2pMessageType.Ack));
            Assert.That(ack.PayloadLength, Is.Zero);
            Assert.That(error.Type, Is.EqualTo(P2pMessageType.Error));
            Assert.That(error.GetPayload(), Is.EqualTo(new byte[] { 2, 0x62, 0x75, 0x73, 0x79 }));
        });
    }

    [Test]
    public void EncodeUsesExactBigEndianWireFormat()
    {
        var encoded = P2pFrameCodec.Encode(
            P2pFrame.CreateData(Sender, new P2pAddress(Recipient), 0x89ABCDEF, [0xAA]));

        Assert.That(
            encoded,
            Is.EqualTo(new byte[] { 1, 1, 0, 0x12, 0x34, 0x56, 0x78, 0x89, 0xAB, 0xCD, 0xEF, 1, 0xAA }));
    }

    [TestCase(new byte[] { 1, 1 })]
    [TestCase(new byte[] { 2, 1, 0, 0, 1, 0, 2, 0, 0, 0, 1, 1, 0xAA })]
    [TestCase(new byte[] { 1, 99, 0, 0, 1, 0, 2, 0, 0, 0, 1, 1, 0xAA })]
    [TestCase(new byte[] { 1, 1, 1, 0, 1, 0, 2, 0, 0, 0, 1, 1, 0xAA })]
    [TestCase(new byte[] { 1, 1, 0, 0, 0, 0, 2, 0, 0, 0, 1, 1, 0xAA })]
    [TestCase(new byte[] { 1, 1, 0, 0xFF, 0xFF, 0, 2, 0, 0, 0, 1, 1, 0xAA })]
    [TestCase(new byte[] { 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 0xAA })]
    [TestCase(new byte[] { 1, 1, 0, 0, 1, 0, 2, 0, 0, 0, 1, 2, 0xAA })]
    [TestCase(new byte[] { 1, 2, 0, 0, 1, 0, 2, 0, 0, 0, 1, 1, 0xAA })]
    [TestCase(new byte[] { 1, 2, 0, 0, 1, 0xFF, 0xFF, 0, 0, 0, 1, 0 })]
    public void DecodeRejectsInvalidFrames(byte[] encoded)
    {
        Assert.That(() => P2pFrameCodec.Decode(encoded), Throws.TypeOf<P2pProtocolException>());
    }

    [Test]
    public void ModelsRejectInvalidValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => new P2pNodeId(0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new P2pNodeId(ushort.MaxValue), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => P2pFrame.CreateData(Sender, new P2pAddress(Recipient), 1, []),
                Throws.TypeOf<ArgumentException>());
            Assert.That(
                () => new P2pOptions { MaxRetries = 11 }.Validate(),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new P2pOptions
                {
                    BackoffMinimum = TimeSpan.FromSeconds(2),
                    BackoffMaximum = TimeSpan.FromSeconds(1)
                }.Validate(),
                Throws.TypeOf<ArgumentException>());
        });
    }

    private static P2pFrame RoundTrip(P2pFrame frame) => P2pFrameCodec.Decode(P2pFrameCodec.Encode(frame));
}
