namespace LoRaP2P.Radio.Rfm9x.Tests;

public sealed class Rfm9xRadioTests
{
    private static readonly bool[] ExpectedResetStates = [true, false];

    [Test]
    public async Task ConfigureWritesEu868ModemProfile()
    {
        var transport = new FakeRfm9xRegisterTransport();
        await using var radio = new Rfm9xRadio(transport);

        await radio.ConfigureAsync(new RadioConfiguration());

        int frequencyValue = Rfm9xFrequency.ToRegisterValue(868_100_000);
        Assert.Multiple(() =>
        {
            Assert.That(transport.ReadByte(0x06), Is.EqualTo((byte)(frequencyValue >> 16)));
            Assert.That(transport.ReadByte(0x07), Is.EqualTo((byte)(frequencyValue >> 8)));
            Assert.That(transport.ReadByte(0x08), Is.EqualTo((byte)frequencyValue));
            Assert.That(transport.ReadByte(0x09), Is.EqualTo(0x8C));
            Assert.That(transport.ReadByte(0x1D), Is.EqualTo(0x72));
            Assert.That(transport.ReadByte(0x1E), Is.EqualTo(0x74));
            Assert.That(transport.ReadByte(0x26), Is.EqualTo(0x04));
            Assert.That(transport.ReadByte(0x39), Is.EqualTo(0x34));
            Assert.That(transport.ReadByte(0x01), Is.EqualTo(0x81));
        });
    }

    [Test]
    public async Task TransmitWritesFifoAndCompletesOnTxDone()
    {
        var transport = new FakeRfm9xRegisterTransport();
        await using var radio = new Rfm9xRadio(transport);
        await radio.ConfigureAsync(new RadioConfiguration());

        Task<TransmitResult> transmission = radio.TransmitAsync(new byte[] { 0x10, 0x20, 0x30 });
        transport.SetRegister(0x12, 0x08);
        transport.SignalDio0();

        TransmitResult result = await transmission;

        Assert.Multiple(() =>
        {
            Assert.That(transport.LastFifoWrite, Is.EqualTo(new byte[] { 0x10, 0x20, 0x30 }));
            Assert.That(transport.ReadByte(0x22), Is.EqualTo(3));
            Assert.That(transport.ReadByte(0x01), Is.EqualTo(0x81));
            Assert.That(result.TimeOnAir, Is.GreaterThan(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task ReceiveReturnsPayloadAndSignalMetrics()
    {
        var transport = new FakeRfm9xRegisterTransport();
        await using var radio = new Rfm9xRadio(transport);
        await radio.ConfigureAsync(new RadioConfiguration());

        Task<ReceivedPacket?> reception = radio.ReceiveSingleAsync(TimeSpan.FromSeconds(1));
        transport.SetRegister(0x10, 0x00);
        transport.SetRegister(0x13, 0x03);
        transport.SetFifoReadData(new byte[] { 0xAA, 0xBB, 0xCC });
        transport.SetRegister(0x19, unchecked((byte)-8));
        transport.SetRegister(0x1A, 100);
        transport.SetRegister(0x1C, 0x40);
        transport.SetRegister(0x12, 0x40);
        transport.SignalDio0();

        ReceivedPacket? packet = await reception;

        Assert.That(packet, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(packet!.Payload, Is.EqualTo(new byte[] { 0xAA, 0xBB, 0xCC }));
            Assert.That(packet.IsCrcValid, Is.True);
            Assert.That(packet.PacketSnrDb, Is.EqualTo(-2));
            Assert.That(packet.PacketRssiDbm, Is.EqualTo(-59));
        });
    }

    [Test]
    public async Task ResetAndProbeUseExpectedHardwareSequence()
    {
        var transport = new FakeRfm9xRegisterTransport();
        await using var radio = new Rfm9xRadio(transport);

        await radio.ResetAsync();
        byte version = await radio.ProbeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(transport.ResetStates, Is.EqualTo(ExpectedResetStates));
            Assert.That(version, Is.EqualTo(0x12));
        });
    }
}