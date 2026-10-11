namespace LoRaP2P.Radio.Rfm9x.Tests;

public sealed class Rfm9xRadioTests : IAsyncDisposable
{
    private static readonly bool[] ExpectedResetStates = [true, false];
    private FakeRfm9xRegisterTransport transport = null!;
    private Rfm9xRadio radio = null!;
    private RadioConfiguration configuration = null!;

    [SetUp]
    public void SetUp()
    {
        this.transport = new FakeRfm9xRegisterTransport();
        this.radio = new Rfm9xRadio(this.transport);
        this.configuration = new RadioConfiguration();
    }

    [TearDown]
    public async Task TearDown()
    {
        await this.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await this.radio.DisposeAsync();
    }

    [Test]
    public async Task ConfigureWritesEu868ModemProfile()
    {
        await this.radio.ConfigureAsync(this.configuration);

        var frequencyValue = Rfm9xFrequency.ToRegisterValue(869_525_000);
        Assert.Multiple(() =>
        {
            Assert.That(this.transport.ReadByte(0x06), Is.EqualTo((byte)(frequencyValue >> 16)));
            Assert.That(this.transport.ReadByte(0x07), Is.EqualTo((byte)(frequencyValue >> 8)));
            Assert.That(this.transport.ReadByte(0x08), Is.EqualTo((byte)frequencyValue));
            Assert.That(this.transport.ReadByte(0x09), Is.EqualTo(0x8C));
            Assert.That(this.transport.ReadByte(0x1D), Is.EqualTo(0x72));
            Assert.That(this.transport.ReadByte(0x1E), Is.EqualTo(0x74));
            Assert.That(this.transport.ReadByte(0x26), Is.EqualTo(0x04));
            Assert.That(this.transport.ReadByte(0x39), Is.EqualTo(0x34));
            Assert.That(this.transport.ReadByte(0x01), Is.EqualTo(0x81));
        });
    }

    [Test]
    public async Task ConfigureWritesMaximumOutputPower()
    {
        await this.radio.ConfigureAsync(this.configuration with { OutputPowerDbm = 17 });

        Assert.That(this.transport.ReadByte(0x09), Is.EqualTo(0x8F));
    }

    [Test]
    public async Task TransmitWritesFifoAndCompletesOnTxDone()
    {
        await this.radio.ConfigureAsync(this.configuration);

        var transmission = this.radio.TransmitAsync(new byte[] { 0x10, 0x20, 0x30 });
        this.transport.SetRegister(0x12, 0x08);
        this.transport.SignalDio0();

        var result = await transmission;

        Assert.Multiple(() =>
        {
            Assert.That(this.transport.LastFifoWrite, Is.EqualTo(new byte[] { 0x10, 0x20, 0x30 }));
            Assert.That(this.transport.ReadByte(0x22), Is.EqualTo(3));
            Assert.That(this.transport.ReadByte(0x01), Is.EqualTo(0x81));
            Assert.That(result.TimeOnAir, Is.GreaterThan(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task ReceiveReturnsPayloadAndSignalMetrics()
    {
        await this.radio.ConfigureAsync(this.configuration);

        var reception = this.radio.ReceiveSingleAsync(TimeSpan.FromSeconds(1));
        this.transport.SetRegister(0x10, 0x00);
        this.transport.SetRegister(0x13, 0x03);
        this.transport.SetFifoReadData([0xAA, 0xBB, 0xCC]);
        this.transport.SetRegister(0x19, unchecked((byte)-8));
        this.transport.SetRegister(0x1A, 100);
        this.transport.SetRegister(0x1C, 0x40);
        this.transport.SetRegister(0x12, 0x40);
        this.transport.SignalDio0();

        var packet = await reception;

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
        await this.radio.ResetAsync();
        var version = await this.radio.ProbeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(this.transport.ResetStates, Is.EqualTo(ExpectedResetStates));
            Assert.That(version, Is.EqualTo(0x12));
        });
    }
}