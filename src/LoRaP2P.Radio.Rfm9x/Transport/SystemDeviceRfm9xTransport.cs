using System.Device.Gpio;
using System.Device.Spi;

namespace LoRaP2P.Radio.Rfm9x.Transport;

public sealed class SystemDeviceRfm9xTransport : IRfm9xRegisterTransport
{
    private const byte ReadMask = 0x7F;
    private const byte WriteMask = 0x80;
    private readonly Lock _spiLock = new();
    private readonly SpiDevice _spiDevice;
    private readonly GpioController _gpioController;
    private readonly int _resetPin;
    private readonly int _dio0Pin;
    private readonly SemaphoreSlim _dio0Signal = new(0, 1);
    private bool _disposed;

    public SystemDeviceRfm9xTransport(RadioConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        var connectionSettings = new SpiConnectionSettings(configuration.SpiBusId, configuration.ChipSelectLine)
        {
            ClockFrequency = 5_000_000,
            DataBitLength = 8,
            Mode = SpiMode.Mode0
        };

        _spiDevice = SpiDevice.Create(connectionSettings);
        _gpioController = new GpioController();
        _resetPin = configuration.ResetPin;
        _dio0Pin = configuration.Dio0Pin;

        try
        {
            _gpioController.OpenPin(_resetPin, PinMode.Output);
            _gpioController.Write(_resetPin, PinValue.High);
            _gpioController.OpenPin(_dio0Pin, PinMode.Input);
            _gpioController.RegisterCallbackForPinValueChangedEvent(
                _dio0Pin,
                PinEventTypes.Rising,
                HandleDio0RisingEdge);
        }
        catch
        {
            _gpioController.Dispose();
            _spiDevice.Dispose();
            throw;
        }
    }

    public byte ReadByte(byte address)
    {
        Span<byte> value = stackalloc byte[1];
        Read(address, value);
        return value[0];
    }

    public void Read(byte address, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateAddress(address);

        var writeBuffer = new byte[destination.Length + 1];
        var readBuffer = new byte[destination.Length + 1];
        writeBuffer[0] = (byte)(address & ReadMask);

        lock (_spiLock)
        {
            _spiDevice.TransferFullDuplex(writeBuffer, readBuffer);
        }

        readBuffer.AsSpan(1).CopyTo(destination);
    }

    public void WriteByte(byte address, byte value)
    {
        Span<byte> source = stackalloc byte[1];
        source[0] = value;
        Write(address, source);
    }

    public void Write(byte address, ReadOnlySpan<byte> source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateAddress(address);

        var writeBuffer = new byte[source.Length + 1];
        writeBuffer[0] = (byte)(address | WriteMask);
        source.CopyTo(writeBuffer.AsSpan(1));

        lock (_spiLock)
        {
            _spiDevice.Write(writeBuffer);
        }
    }

    public void SetReset(bool asserted)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _gpioController.Write(_resetPin, asserted ? PinValue.Low : PinValue.High);
    }

    public void ClearDio0Signal()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (_dio0Signal.Wait(0))
        {
        }
    }

    public ValueTask WaitForDio0RisingEdgeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new ValueTask(_dio0Signal.WaitAsync(cancellationToken));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gpioController.UnregisterCallbackForPinValueChangedEvent(_dio0Pin, HandleDio0RisingEdge);
        _gpioController.Dispose();
        _spiDevice.Dispose();
        _dio0Signal.Dispose();
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
        if (_disposed || eventArgs.ChangeType != PinEventTypes.Rising)
        {
            return;
        }

        try
        {
            if (_dio0Signal.CurrentCount == 0)
            {
                _dio0Signal.Release();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }
}