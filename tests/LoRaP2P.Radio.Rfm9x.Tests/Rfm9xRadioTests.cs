namespace LoRaP2P.Radio.Rfm9x.Tests;

public sealed class Rfm9xRadioTests : IAsyncDisposable
{
    private static readonly bool[] ExpectedResetStates = [true, false];
    private FakeRfm9xRegisterTransport _transport = null!;
    private Rfm9xRadio _radio = null!;
    private RadioConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        _transport = new FakeRfm9xRegisterTransport();
        _radio = new Rfm9xRadio(_transport);
        _configuration = new RadioConfiguration();
    }

    [TearDown]
    public async Task TearDown()
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _radio.DisposeAsync();
    }

    [Test]
    public async Task ConfigureWritesEu868ModemProfile()
    {
        await _radio.ConfigureAsync(_configuration);

        var frequencyValue = Rfm9xFrequency.ToRegisterValue(869_525_000);
        Assert.Multiple(() =>
        {
            Assert.That(_transport.ReadByte(0x06), Is.EqualTo((byte)(frequencyValue >> 16)));
            Assert.That(_transport.ReadByte(0x07), Is.EqualTo((byte)(frequencyValue >> 8)));
            Assert.That(_transport.ReadByte(0x08), Is.EqualTo((byte)frequencyValue));
            Assert.That(_transport.ReadByte(0x09), Is.EqualTo(0x8C));
            Assert.That(_transport.ReadByte(0x1D), Is.EqualTo(0x72));
            Assert.That(_transport.ReadByte(0x1E), Is.EqualTo(0x74));
            Assert.That(_transport.ReadByte(0x26), Is.EqualTo(0x04));
            Assert.That(_transport.ReadByte(0x39), Is.EqualTo(0x34));
            Assert.That(_transport.ReadByte(0x01), Is.EqualTo(0x81));
        });
    }

    [Test]
    public async Task TransmitWritesFifoAndCompletesOnTxDone()
    {
        await _radio.ConfigureAsync(_configuration);

        var transmission = _radio.TransmitAsync(new byte[] { 0x10, 0x20, 0x30 });
        _transport.SetRegister(0x12, 0x08);
        _transport.SignalDio0();

        var result = await transmission;

        Assert.Multiple(() =>
        {
            Assert.That(_transport.LastFifoWrite, Is.EqualTo(new byte[] { 0x10, 0x20, 0x30 }));
            Assert.That(_transport.ReadByte(0x22), Is.EqualTo(3));
            Assert.That(_transport.ReadByte(0x01), Is.EqualTo(0x81));
            Assert.That(result.TimeOnAir, Is.GreaterThan(TimeSpan.Zero));
        });
    }

    [Test]
    public async Task ReceiveReturnsPayloadAndSignalMetrics()
    {
        await _radio.ConfigureAsync(_configuration);

        var reception = _radio.ReceiveSingleAsync(TimeSpan.FromSeconds(1));
        _transport.SetRegister(0x10, 0x00);
        _transport.SetRegister(0x13, 0x03);
        _transport.SetFifoReadData([0xAA, 0xBB, 0xCC]);
        _transport.SetRegister(0x19, unchecked((byte)-8));
        _transport.SetRegister(0x1A, 100);
        _transport.SetRegister(0x1C, 0x40);
        _transport.SetRegister(0x12, 0x40);
        _transport.SignalDio0();

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
        await _radio.ResetAsync();
        var version = await _radio.ProbeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_transport.ResetStates, Is.EqualTo(ExpectedResetStates));
            Assert.That(version, Is.EqualTo(0x12));
        });
    }
}