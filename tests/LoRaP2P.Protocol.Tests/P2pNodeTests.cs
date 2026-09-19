using LoRaP2P.Protocol.Frames;
using LoRaP2P.Protocol.Reliability;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Protocol.Tests;

public sealed class P2pNodeTests
{
    private static readonly P2pNodeId Local = new(1);
    private static readonly P2pNodeId Peer = new(2);

    [Test]
    public async Task SendCompletesForMatchingAck()
    {
        var radio = new ScriptedLoraRadio();
        radio.EnqueuePacket(Packet(P2pFrame.CreateAck(Peer, Local, 42)));
        using var node = CreateNode(radio);

        var result = await node.SendAsync(Peer, new byte[] { 0xAA });

        Assert.Multiple(() =>
        {
            Assert.That(result.SequenceNumber, Is.EqualTo(42));
            Assert.That(result.Attempts, Is.EqualTo(1));
            Assert.That(radio.Transmissions, Has.Count.EqualTo(1));
            Assert.That(node.Statistics.GetSnapshot().AcksReceived, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SendIgnoresMismatchedAcks()
    {
        var radio = new ScriptedLoraRadio();
        radio.EnqueuePacket(Packet(P2pFrame.CreateAck(new P2pNodeId(3), Local, 42)));
        radio.EnqueuePacket(Packet(P2pFrame.CreateAck(Peer, Local, 41)));
        radio.EnqueuePacket(Packet(P2pFrame.CreateAck(Peer, Local, 42)));
        using var node = CreateNode(radio);

        var result = await node.SendAsync(Peer, new byte[] { 1 });

        Assert.That(result.Attempts, Is.EqualTo(1));
        Assert.That(node.Statistics.GetSnapshot().AcksReceived, Is.EqualTo(3));
    }

    [Test]
    public async Task TimeoutRetriesSameFrameThenSucceeds()
    {
        var radio = new ScriptedLoraRadio();
        radio.EnqueueTimeout();
        radio.EnqueuePacket(Packet(P2pFrame.CreateAck(Peer, Local, 42)));
        var delay = new RecordingDelay();
        using var node = CreateNode(radio, delay: delay);

        var result = await node.SendAsync(Peer, new byte[] { 1, 2 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Attempts, Is.EqualTo(2));
            Assert.That(radio.Transmissions, Has.Count.EqualTo(2));
            Assert.That(radio.Transmissions[1], Is.EqualTo(radio.Transmissions[0]));
            Assert.That(delay.Delays, Has.Count.EqualTo(1));
            Assert.That(delay.Delays[0], Is.InRange(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(1)));
            Assert.That(node.Statistics.GetSnapshot().Retries, Is.EqualTo(1));
        });
    }

    [Test]
    public void FourTimeoutsProduceDeliveryFailure()
    {
        var radio = new ScriptedLoraRadio();
        for (var index = 0; index < 4; index++)
        {
            radio.EnqueueTimeout();
        }

        using var node = CreateNode(radio);

        Assert.That(
            async () => await node.SendAsync(Peer, new byte[] { 1 }),
            Throws.TypeOf<P2pDeliveryException>()
                .And.Property(nameof(P2pDeliveryException.Attempts)).EqualTo(4));
        Assert.Multiple(() =>
        {
            Assert.That(radio.Transmissions, Has.Count.EqualTo(4));
            Assert.That(node.Statistics.GetSnapshot().AckTimeouts, Is.EqualTo(4));
            Assert.That(node.Statistics.GetSnapshot().DeliveryFailures, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task BroadcastTransmitsOnceWithoutReceiving()
    {
        var radio = new ScriptedLoraRadio();
        using var node = CreateNode(radio);

        var result = await node.BroadcastAsync(new byte[] { 0xAA });
        var frame = P2pFrameCodec.Decode(radio.Transmissions.Single());

        Assert.Multiple(() =>
        {
            Assert.That(result.SequenceNumber, Is.EqualTo(42));
            Assert.That(frame.Recipient, Is.EqualTo(P2pAddress.Broadcast));
            Assert.That(radio.ReceiveTimeouts, Is.Empty);
        });
    }

    [Test]
    public async Task DuplicateDataIsDeliveredOnceAndAcknowledgedTwice()
    {
        using var cancellation = new CancellationTokenSource();
        var radio = new ScriptedLoraRadio();
        var data = P2pFrame.CreateData(Peer, new P2pAddress(Local), 9, [0x10]);
        radio.EnqueuePacket(Packet(data));
        radio.EnqueuePacket(Packet(data));
        radio.TransmitHandler = (_, _) =>
        {
            if (radio.Transmissions.Count == 2)
            {
                cancellation.Cancel();
            }

            return Task.FromResult(new TransmitResult(TimeSpan.Zero, DateTimeOffset.UnixEpoch));
        };

        using var node = CreateNode(radio);
        var delivered = new List<P2pReceivedMessage>();
        node.DataReceived += delivered.Add;

        Assert.That(
            async () => await node.ListenAsync(cancellation.Token),
            Throws.TypeOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(delivered, Has.Count.EqualTo(1));
            Assert.That(radio.Transmissions, Has.Count.EqualTo(2));
            Assert.That(node.Statistics.GetSnapshot().Duplicates, Is.EqualTo(1));
            Assert.That(node.Statistics.GetSnapshot().AcksSent, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task BroadcastIsDeliveredWithoutAck()
    {
        using var cancellation = new CancellationTokenSource();
        var radio = new ScriptedLoraRadio();
        radio.EnqueuePacket(Packet(P2pFrame.CreateData(Peer, P2pAddress.Broadcast, 7, [0x20])));
        using var node = CreateNode(radio);
        var delivered = new List<P2pReceivedMessage>();
        node.DataReceived += message =>
        {
            delivered.Add(message);
            cancellation.Cancel();
        };

        Assert.That(
            async () => await node.ListenAsync(cancellation.Token),
            Throws.TypeOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(delivered, Has.Count.EqualTo(1));
            Assert.That(radio.Transmissions, Is.Empty);
        });
    }

    [Test]
    public async Task AckWaitProcessesAndAcknowledgesOtherData()
    {
        var radio = new ScriptedLoraRadio();
        var otherPeer = new P2pNodeId(3);
        radio.EnqueuePacket(Packet(P2pFrame.CreateData(otherPeer, new P2pAddress(Local), 100, [0x33])));
        radio.EnqueuePacket(Packet(P2pFrame.CreateAck(Peer, Local, 42)));
        using var node = CreateNode(radio);
        var delivered = new List<P2pReceivedMessage>();
        node.DataReceived += delivered.Add;

        var result = await node.SendAsync(Peer, new byte[] { 0x44 });

        Assert.Multiple(() =>
        {
            Assert.That(result.Attempts, Is.EqualTo(1));
            Assert.That(delivered.Select(static message => message.Sender), Is.EqualTo(new[] { otherPeer }));
            Assert.That(radio.Transmissions, Has.Count.EqualTo(2));
            Assert.That(P2pFrameCodec.Decode(radio.Transmissions[1]).Type, Is.EqualTo(P2pMessageType.Ack));
        });
    }

    [Test]
    public async Task ListenCountsCrcInvalidAndForeignFrames()
    {
        using var cancellation = new CancellationTokenSource();
        var radio = new ScriptedLoraRadio();
        radio.EnqueuePacket(new ReceivedPacket([1], false, -90, -2));
        radio.EnqueuePacket(new ReceivedPacket([1], true, -90, -2));
        radio.EnqueuePacket(Packet(P2pFrame.CreateData(Peer, new P2pAddress(new P2pNodeId(4)), 1, [1])));
        radio.EnqueuePacket(
            Packet(P2pFrame.CreateData(Peer, P2pAddress.Broadcast, 2, [1])),
            cancellation.Cancel);
        using var node = CreateNode(radio);

        Assert.That(
            async () => await node.ListenAsync(cancellation.Token),
            Throws.TypeOf<OperationCanceledException>());
        var statistics = node.Statistics.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(statistics.CrcErrors, Is.EqualTo(1));
            Assert.That(statistics.InvalidFramesDropped, Is.EqualTo(1));
            Assert.That(statistics.ForeignFramesDropped, Is.EqualTo(1));
        });
    }

    [Test]
    public void CancellationDuringAckWaitIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        var radio = new ScriptedLoraRadio
        {
            TransmitHandler = (_, _) =>
            {
                cancellation.Cancel();
                return Task.FromResult(new TransmitResult(TimeSpan.Zero, DateTimeOffset.UnixEpoch));
            }
        };
        using var node = CreateNode(radio);

        Assert.That(
            async () => await node.SendAsync(Peer, new byte[] { 1 }, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void CancellationDuringTransmitIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        var radio = new ScriptedLoraRadio
        {
            TransmitHandler = (_, _) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<TransmitResult>(cancellation.Token);
            }
        };
        using var node = CreateNode(radio);

        Assert.That(
            async () => await node.SendAsync(Peer, new byte[] { 1 }, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void CancellationDuringBackoffIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        var radio = new ScriptedLoraRadio();
        radio.EnqueueTimeout();
        using var node = CreateNode(radio, new CancelingDelay(cancellation));

        Assert.That(
            async () => await node.SendAsync(Peer, new byte[] { 1 }, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(radio.Transmissions, Has.Count.EqualTo(1));
    }

    private static P2pNode CreateNode(ScriptedLoraRadio radio, IP2pDelay? delay = null) =>
        new(
            radio,
            Local,
            new P2pOptions(),
            sequences: new SequenceNumberGenerator(42),
            random: new StubRandom(nextInt32: int.MaxValue / 2),
            delay: delay ?? new RecordingDelay());

    private static ReceivedPacket Packet(P2pFrame frame) =>
        new(P2pFrameCodec.Encode(frame), true, -70, 5.25);
}
