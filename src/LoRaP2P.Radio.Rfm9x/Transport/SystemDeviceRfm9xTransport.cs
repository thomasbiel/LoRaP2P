using System.Device.Gpio;
using System.Device.Spi;

namespace LoRaP2P.Radio.Rfm9x.Transport;

public sealed class SystemDeviceRfm9xTransport : IRfm9xRegisterTransport
{
    private const byte ReadMask = 0x7F;
    private const byte WriteMask = 0x80;
    private readonly Lock spiLock = new();
    private readonly SpiDevice spiDevice;
    private readonly GpioController gpioController;
    private readonly int resetPin;
    private readonly int dio0Pin;
    private readonly bool dio0EventsEnabled;
    private readonly SemaphoreSlim dio0Signal = new(0, 1);
    private bool disposed;

    public SystemDeviceRfm9xTransport(
        RadioConfiguration configuration,
        bool enableDio0Events = true)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        var connectionSettings = new SpiConnectionSettings(configuration.SpiBusId, configuration.ChipSelectLine)
        {
            ClockFrequency = 5_000_000,
            DataBitLength = 8,
            Mode = SpiMode.Mode0
        };

        this.spiDevice = SpiDevice.Create(connectionSettings);
        this.gpioController = new GpioController();
        this.resetPin = configuration.ResetPin;
        this.dio0Pin = configuration.Dio0Pin;
        this.dio0EventsEnabled = enableDio0Events;

        try
        {
            this.gpioController.OpenPin(this.resetPin, PinMode.Output);
            this.gpioController.Write(this.resetPin, PinValue.High);
            if (this.dio0EventsEnabled)
            {
                this.gpioController.OpenPin(this.dio0Pin, PinMode.Input);
                this.gpioController.RegisterCallbackForPinValueChangedEvent(
                    this.dio0Pin,
                    PinEventTypes.Rising,
                    this.HandleDio0RisingEdge);
            }
        }
        catch
        {
            this.gpioController.Dispose();
            this.spiDevice.Dispose();
            throw;
        }
    }

    public byte ReadByte(byte address)
    {
        Span<byte> value = stackalloc byte[1];
        this.Read(address, value);
        return value[0];
    }

    public void Read(byte address, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ValidateAddress(address);

        var writeBuffer = new byte[destination.Length + 1];
        var readBuffer = new byte[destination.Length + 1];
        writeBuffer[0] = (byte)(address & ReadMask);

        lock (this.spiLock)
        {
            this.spiDevice.TransferFullDuplex(writeBuffer, readBuffer);
        }

        readBuffer.AsSpan(1).CopyTo(destination);
    }

    public void WriteByte(byte address, byte value)
    {
        Span<byte> source = stackalloc byte[1];
        source[0] = value;
        this.Write(address, source);
    }

    public void Write(byte address, ReadOnlySpan<byte> source)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ValidateAddress(address);

        var writeBuffer = new byte[source.Length + 1];
        writeBuffer[0] = (byte)(address | WriteMask);
        source.CopyTo(writeBuffer.AsSpan(1));

        lock (this.spiLock)
        {
            this.spiDevice.Write(writeBuffer);
        }
    }

    public void SetReset(bool asserted)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        this.gpioController.Write(this.resetPin, asserted ? PinValue.Low : PinValue.High);
    }

    public void ClearDio0Signal()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        while (this.dio0Signal.Wait(0))
        {
        }
    }

    public ValueTask WaitForDio0RisingEdgeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (!this.dio0EventsEnabled)
        {
            throw new InvalidOperationException("DIO0 event monitoring is disabled.");
        }

        return new ValueTask(this.dio0Signal.WaitAsync(cancellationToken));
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        if (this.dio0EventsEnabled)
        {
            this.gpioController.UnregisterCallbackForPinValueChangedEvent(this.dio0Pin, this.HandleDio0RisingEdge);
        }

        this.gpioController.Dispose();
        this.spiDevice.Dispose();
        this.dio0Signal.Dispose();
    }

    private static void ValidateAddress(byte address)
    {
        if (address > ReadMask)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
    }

    private void HandleDio0RisingEdge(object sender, PinValueChangedEventArgs eventArgs)
    {
        if (this.disposed || eventArgs.ChangeType != PinEventTypes.Rising)
        {
            return;
        }

        try
        {
            if (this.dio0Signal.CurrentCount == 0)
            {
                this.dio0Signal.Release();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}