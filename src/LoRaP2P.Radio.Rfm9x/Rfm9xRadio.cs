using LoRaP2P.Radio.Rfm9x.Transport;

namespace LoRaP2P.Radio.Rfm9x;

public sealed class Rfm9xRadio : ILoraRadio
{
    private readonly IRfm9xRegisterTransport _transport;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly TimeProvider _timeProvider;
    private RadioConfiguration _configuration = new();
    private DateTimeOffset _nextTransmitAt = DateTimeOffset.MinValue;
    private RadioMode _mode = RadioMode.Sleep;
    private bool _configured;
    private bool _disposed;

    public Rfm9xRadio(IRfm9xRegisterTransport transport, TimeProvider? timeProvider = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _configured = false;
            _transport.SetReset(asserted: true);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _transport.SetReset(asserted: false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), _timeProvider, cancellationToken).ConfigureAwait(false);
            _mode = RadioMode.Sleep;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<byte> ProbeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = _transport.ReadByte((byte)Rfm9xRegister.Version);
            if (version != Rfm9xBits.VersionExpected)
            {
                throw new Rfm9xException($"SX1276/RFM95 not detected. Expected version 0x12, read 0x{version:X2}.");
            }

            return version;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task ConfigureAsync(RadioConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _configuration = configuration;
            SetMode(RadioMode.Sleep);
            WriteFrequency(configuration.FrequencyHertz);
            _transport.WriteByte((byte)Rfm9xRegister.PaConfig, (byte)(0x80 | (configuration.OutputPowerDbm - 2)));
            _transport.WriteByte((byte)Rfm9xRegister.Lna, 0x23);
            _transport.WriteByte((byte)Rfm9xRegister.FifoTxBaseAddress, 0x00);
            _transport.WriteByte((byte)Rfm9xRegister.FifoRxBaseAddress, 0x00);
            _transport.WriteByte((byte)Rfm9xRegister.MaxPayloadLength, 0xFF);
            WriteModemConfiguration(configuration);
            WritePreamble(configuration.PreambleSymbols);
            WriteIqConfiguration(configuration.InvertIq);
            _transport.WriteByte((byte)Rfm9xRegister.SyncWord, configuration.SyncWord);
            _transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            SetMode(RadioMode.Standby);
            _configured = true;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<TransmitResult> TransmitAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureConfigured();
        if (payload.Length is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload must contain 1 through 255 bytes.");
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WaitForDutyCycleAsync(cancellationToken).ConfigureAwait(false);
            var timeOnAir = Rfm9xTimeOnAir.Calculate(payload.Length, _configuration);

            _transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            _transport.ClearDio0Signal();
            _transport.WriteByte((byte)Rfm9xRegister.FifoTxBaseAddress, 0x00);
            _transport.WriteByte((byte)Rfm9xRegister.FifoAddressPointer, 0x00);
            _transport.Write((byte)Rfm9xRegister.Fifo, payload.Span);
            _transport.WriteByte((byte)Rfm9xRegister.PayloadLength, checked((byte)payload.Length));
            WriteDio0Mapping(Rfm9xBits.Dio0TxDone);
            SetMode(RadioMode.Transmit);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(_configuration.OperationTimeout);

            try
            {
                await _transport.WaitForDio0RisingEdgeAsync(timeoutSource.Token).ConfigureAwait(false);
                var irqFlags = _transport.ReadByte((byte)Rfm9xRegister.IrqFlags);
                if ((irqFlags & Rfm9xBits.IrqTxDone) == 0)
                {
                    throw new Rfm9xException($"DIO0 rose without TxDone. IRQ flags: 0x{irqFlags:X2}.");
                }

                var completedAt = _timeProvider.GetUtcNow();
                var offAirSeconds = timeOnAir.TotalSeconds * ((1 / _configuration.DutyCycle) - 1);
                _nextTransmitAt = completedAt + TimeSpan.FromSeconds(offAirSeconds);
                return new TransmitResult(timeOnAir, completedAt);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("RFM95 transmission did not produce TxDone before the timeout.", exception);
            }
            finally
            {
                SetMode(RadioMode.Standby);
                _transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<ReceivedPacket?> ReceiveSingleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureConfigured();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            _transport.ClearDio0Signal();
            WriteDio0Mapping(Rfm9xBits.Dio0RxDone);
            SetMode(RadioMode.ReceiveSingle);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                await _transport.WaitForDio0RisingEdgeAsync(timeoutSource.Token).ConfigureAwait(false);
                var irqFlags = _transport.ReadByte((byte)Rfm9xRegister.IrqFlags);
                if ((irqFlags & Rfm9xBits.IrqRxTimeout) != 0)
                {
                    return null;
                }

                if ((irqFlags & Rfm9xBits.IrqRxDone) == 0)
                {
                    throw new Rfm9xException($"DIO0 rose without RxDone. IRQ flags: 0x{irqFlags:X2}.");
                }

                return ReadReceivedPacket(irqFlags);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            finally
            {
                SetMode(RadioMode.Standby);
                _transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<RadioStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = _transport.ReadByte((byte)Rfm9xRegister.Version);
            var irqFlags = _transport.ReadByte((byte)Rfm9xRegister.IrqFlags);
            return new RadioStatus(version, _mode, irqFlags, _configuration);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<byte, byte>> ReadRegistersAsync(
        byte startAddress,
        int count,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (count is < 1 or > 128 || startAddress + count > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        await _operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = new Dictionary<byte, byte>(count);
            for (var offset = 0; offset < count; offset++)
            {
                var address = checked((byte)(startAddress + offset));
                result.Add(address, _transport.ReadByte(address));
            }

            return result;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _transport.Dispose();
        _operationLock.Dispose();
        return ValueTask.CompletedTask;
    }

    private void WriteFrequency(double frequencyHertz)
    {
        var registerValue = Rfm9xFrequency.ToRegisterValue(frequencyHertz);
        _transport.WriteByte((byte)Rfm9xRegister.FrequencyMsb, (byte)(registerValue >> 16));
        _transport.WriteByte((byte)Rfm9xRegister.FrequencyMid, (byte)(registerValue >> 8));
        _transport.WriteByte((byte)Rfm9xRegister.FrequencyLsb, (byte)registerValue);
    }

    private void WriteModemConfiguration(RadioConfiguration configuration)
    {
        byte bandwidthBits = configuration.Bandwidth switch
        {
            SignalBandwidth.Khz125 => 0x70,
            SignalBandwidth.Khz250 => 0x80,
            SignalBandwidth.Khz500 => 0x90,
            _ => throw new ArgumentOutOfRangeException(nameof(configuration))
        };
        
        var codingRateBits = (byte)(((int)configuration.CodingRate - 4) << 1);
        var modemConfig1 = (byte)(bandwidthBits | codingRateBits | (configuration.ImplicitHeader ? 0x01 : 0x00));
        var modemConfig2 = (byte)(((int)configuration.SpreadingFactor << 4) | (configuration.PayloadCrcEnabled ? 0x04 : 0x00));

        var symbolDurationSeconds = Math.Pow(2, (int)configuration.SpreadingFactor) / (int)configuration.Bandwidth;
        var modemConfig3 = (byte)(0x04 | (symbolDurationSeconds > 0.016 ? 0x08 : 0x00));

        _transport.WriteByte((byte)Rfm9xRegister.ModemConfig1, modemConfig1);
        _transport.WriteByte((byte)Rfm9xRegister.ModemConfig2, modemConfig2);
        _transport.WriteByte((byte)Rfm9xRegister.ModemConfig3, modemConfig3);
    }

    private void WritePreamble(ushort preambleSymbols)
    {
        _transport.WriteByte((byte)Rfm9xRegister.PreambleMsb, (byte)(preambleSymbols >> 8));
        _transport.WriteByte((byte)Rfm9xRegister.PreambleLsb, (byte)preambleSymbols);
    }

    private void WriteIqConfiguration(bool invertIq)
    {
        _transport.WriteByte((byte)Rfm9xRegister.InvertIq, invertIq ? (byte)0x66 : (byte)0x27);
        _transport.WriteByte((byte)Rfm9xRegister.InvertIq2, invertIq ? (byte)0x19 : (byte)0x1D);
    }

    private void WriteDio0Mapping(byte mapping)
    {
        var currentValue = _transport.ReadByte((byte)Rfm9xRegister.DioMapping1);
        _transport.WriteByte((byte)Rfm9xRegister.DioMapping1, (byte)((currentValue & 0x3F) | mapping));
    }

    private ReceivedPacket ReadReceivedPacket(byte irqFlags)
    {
        var currentAddress = _transport.ReadByte((byte)Rfm9xRegister.FifoRxCurrentAddress);
        var payloadLength = _transport.ReadByte((byte)Rfm9xRegister.RxByteCount);
        _transport.WriteByte((byte)Rfm9xRegister.FifoAddressPointer, currentAddress);

        var payload = new byte[payloadLength];
        _transport.Read((byte)Rfm9xRegister.Fifo, payload);

        var packetSnrDb = unchecked((sbyte)_transport.ReadByte((byte)Rfm9xRegister.PacketSnr)) * 0.25;
        int rawRssi = _transport.ReadByte((byte)Rfm9xRegister.PacketRssi);
        var packetRssiDbm = (int)Math.Round(-157 + rawRssi + Math.Min(packetSnrDb, 0));
        var hasPayloadCrc = (_transport.ReadByte((byte)Rfm9xRegister.HopChannel) & Rfm9xBits.HopChannelPayloadCrc) != 0;
        var isCrcValid = (irqFlags & Rfm9xBits.IrqPayloadCrcError) == 0 && (!_configuration.PayloadCrcEnabled || hasPayloadCrc);

        return new ReceivedPacket(payload, isCrcValid, packetRssiDbm, packetSnrDb);
    }

    private void SetMode(RadioMode mode)
    {
        var modeBits = mode switch
        {
            RadioMode.Sleep => Rfm9xBits.ModeSleep,
            RadioMode.Standby => Rfm9xBits.ModeStandby,
            RadioMode.Transmit => Rfm9xBits.ModeTransmit,
            RadioMode.ReceiveSingle => Rfm9xBits.ModeReceiveSingle,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        
        var lowFrequencyBit = _configuration.FrequencyHertz < 525_000_000 ? Rfm9xBits.LowFrequencyMode : (byte)0;
        _transport.WriteByte((byte)Rfm9xRegister.OpMode, (byte)(Rfm9xBits.LongRangeMode | lowFrequencyBit | modeBits));
        _mode = mode;
    }

    private async Task WaitForDutyCycleAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_nextTransmitAt > now)
        {
            await Task.Delay(_nextTransmitAt - now, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private void EnsureConfigured()
    {
        if (!_configured)
        {
            throw new InvalidOperationException("ConfigureAsync must complete before radio operation.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}