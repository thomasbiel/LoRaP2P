using LoRaP2P.Radio.Rfm9x.Transport;

namespace LoRaP2P.Radio.Rfm9x;

public sealed class Rfm9xRadio : ILoraRadio
{
    private readonly IRfm9xRegisterTransport transport;
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private readonly TimeProvider timeProvider;
    private RadioConfiguration configuration = new();
    private DateTimeOffset nextTransmitAt = DateTimeOffset.MinValue;
    private RadioMode mode = RadioMode.Sleep;
    private bool configured;
    private bool disposed;

    public Rfm9xRadio(IRfm9xRegisterTransport transport, TimeProvider? timeProvider = null)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.configured = false;
            this.transport.SetReset(asserted: true);
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1), this.timeProvider, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                this.transport.SetReset(asserted: false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(5), this.timeProvider, cancellationToken).ConfigureAwait(false);
            this.mode = RadioMode.Sleep;
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    public async Task<byte> ProbeAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = this.transport.ReadByte((byte)Rfm9xRegister.Version);
            if (version != Rfm9xBits.VersionExpected)
            {
                throw new Rfm9xException($"SX1276/RFM95 not detected. Expected version 0x12, read 0x{version:X2}.");
            }

            return version;
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    public async Task ConfigureAsync(RadioConfiguration configuration, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.configuration = configuration;
            this.SetMode(RadioMode.Sleep);
            this.WriteFrequency(configuration.FrequencyHertz);
            this.transport.WriteByte((byte)Rfm9xRegister.PaConfig, (byte)(0x80 | (configuration.OutputPowerDbm - 2)));
            this.transport.WriteByte((byte)Rfm9xRegister.Lna, 0x23);
            this.transport.WriteByte((byte)Rfm9xRegister.FifoTxBaseAddress, 0x00);
            this.transport.WriteByte((byte)Rfm9xRegister.FifoRxBaseAddress, 0x00);
            this.transport.WriteByte((byte)Rfm9xRegister.MaxPayloadLength, 0xFF);
            this.WriteModemConfiguration(configuration);
            this.WritePreamble(configuration.PreambleSymbols);
            this.WriteIqConfiguration(configuration.InvertIq);
            this.transport.WriteByte((byte)Rfm9xRegister.SyncWord, configuration.SyncWord);
            this.transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            this.SetMode(RadioMode.Standby);
            this.configured = true;
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    public async Task<TransmitResult> TransmitAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        this.EnsureConfigured();
        if (payload.Length is < 1 or > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(payload), "Payload must contain 1 through 255 bytes.");
        }

        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await this.WaitForDutyCycleAsync(cancellationToken).ConfigureAwait(false);
            var timeOnAir = Rfm9xTimeOnAir.Calculate(payload.Length, this.configuration);

            this.transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            this.transport.ClearDio0Signal();
            this.transport.WriteByte((byte)Rfm9xRegister.FifoTxBaseAddress, 0x00);
            this.transport.WriteByte((byte)Rfm9xRegister.FifoAddressPointer, 0x00);
            this.transport.Write((byte)Rfm9xRegister.Fifo, payload.Span);
            this.transport.WriteByte((byte)Rfm9xRegister.PayloadLength, checked((byte)payload.Length));
            this.WriteDio0Mapping(Rfm9xBits.Dio0TxDone);
            this.SetMode(RadioMode.Transmit);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(this.configuration.OperationTimeout);

            try
            {
                await this.transport.WaitForDio0RisingEdgeAsync(timeoutSource.Token).ConfigureAwait(false);
                var irqFlags = this.transport.ReadByte((byte)Rfm9xRegister.IrqFlags);
                if ((irqFlags & Rfm9xBits.IrqTxDone) == 0)
                {
                    throw new Rfm9xException($"DIO0 rose without TxDone. IRQ flags: 0x{irqFlags:X2}.");
                }

                var completedAt = this.timeProvider.GetUtcNow();
                var offAirSeconds = timeOnAir.TotalSeconds * ((1 / this.configuration.DutyCycle) - 1);
                this.nextTransmitAt = completedAt + TimeSpan.FromSeconds(offAirSeconds);
                return new TransmitResult(timeOnAir, completedAt);
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("RFM95 transmission did not produce TxDone before the timeout.", exception);
            }
            finally
            {
                this.SetMode(RadioMode.Standby);
                this.transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            }
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    public async Task<ReceivedPacket?> ReceiveSingleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        this.EnsureConfigured();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            this.transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            this.transport.ClearDio0Signal();
            this.WriteDio0Mapping(Rfm9xBits.Dio0RxDone);
            this.SetMode(RadioMode.ReceiveSingle);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            try
            {
                await this.transport.WaitForDio0RisingEdgeAsync(timeoutSource.Token).ConfigureAwait(false);
                var irqFlags = this.transport.ReadByte((byte)Rfm9xRegister.IrqFlags);
                if ((irqFlags & Rfm9xBits.IrqRxTimeout) != 0)
                {
                    return null;
                }

                if ((irqFlags & Rfm9xBits.IrqRxDone) == 0)
                {
                    throw new Rfm9xException($"DIO0 rose without RxDone. IRQ flags: 0x{irqFlags:X2}.");
                }

                return this.ReadReceivedPacket(irqFlags);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            finally
            {
                this.SetMode(RadioMode.Standby);
                this.transport.WriteByte((byte)Rfm9xRegister.IrqFlags, Rfm9xBits.IrqClearAll);
            }
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    public async Task<RadioStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var version = this.transport.ReadByte((byte)Rfm9xRegister.Version);
            var irqFlags = this.transport.ReadByte((byte)Rfm9xRegister.IrqFlags);
            return new RadioStatus(version, this.mode, irqFlags, this.configuration);
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    public async Task<IReadOnlyDictionary<byte, byte>> ReadRegistersAsync(
        byte startAddress,
        int count,
        CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        if (count is < 1 or > 128 || startAddress + count > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        await this.operationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = new Dictionary<byte, byte>(count);
            for (var offset = 0; offset < count; offset++)
            {
                var address = checked((byte)(startAddress + offset));
                result.Add(address, this.transport.ReadByte(address));
            }

            return result;
        }
        finally
        {
            this.operationLock.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        if (this.disposed)
        {
            return ValueTask.CompletedTask;
        }

        this.disposed = true;
        this.transport.Dispose();
        this.operationLock.Dispose();
        return ValueTask.CompletedTask;
    }

    private void WriteFrequency(double frequencyHertz)
    {
        var registerValue = Rfm9xFrequency.ToRegisterValue(frequencyHertz);
        this.transport.WriteByte((byte)Rfm9xRegister.FrequencyMsb, (byte)(registerValue >> 16));
        this.transport.WriteByte((byte)Rfm9xRegister.FrequencyMid, (byte)(registerValue >> 8));
        this.transport.WriteByte((byte)Rfm9xRegister.FrequencyLsb, (byte)registerValue);
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

        this.transport.WriteByte((byte)Rfm9xRegister.ModemConfig1, modemConfig1);
        this.transport.WriteByte((byte)Rfm9xRegister.ModemConfig2, modemConfig2);
        this.transport.WriteByte((byte)Rfm9xRegister.ModemConfig3, modemConfig3);
    }

    private void WritePreamble(ushort preambleSymbols)
    {
        this.transport.WriteByte((byte)Rfm9xRegister.PreambleMsb, (byte)(preambleSymbols >> 8));
        this.transport.WriteByte((byte)Rfm9xRegister.PreambleLsb, (byte)preambleSymbols);
    }

    private void WriteIqConfiguration(bool invertIq)
    {
        this.transport.WriteByte((byte)Rfm9xRegister.InvertIq, invertIq ? (byte)0x66 : (byte)0x27);
        this.transport.WriteByte((byte)Rfm9xRegister.InvertIq2, invertIq ? (byte)0x19 : (byte)0x1D);
    }

    private void WriteDio0Mapping(byte mapping)
    {
        var currentValue = this.transport.ReadByte((byte)Rfm9xRegister.DioMapping1);
        this.transport.WriteByte((byte)Rfm9xRegister.DioMapping1, (byte)((currentValue & 0x3F) | mapping));
    }

    private ReceivedPacket ReadReceivedPacket(byte irqFlags)
    {
        var currentAddress = this.transport.ReadByte((byte)Rfm9xRegister.FifoRxCurrentAddress);
        var payloadLength = this.transport.ReadByte((byte)Rfm9xRegister.RxByteCount);
        this.transport.WriteByte((byte)Rfm9xRegister.FifoAddressPointer, currentAddress);

        var payload = new byte[payloadLength];
        this.transport.Read((byte)Rfm9xRegister.Fifo, payload);

        var packetSnrDb = unchecked((sbyte)this.transport.ReadByte((byte)Rfm9xRegister.PacketSnr)) * 0.25;
        int rawRssi = this.transport.ReadByte((byte)Rfm9xRegister.PacketRssi);
        var packetRssiDbm = (int)Math.Round(-157 + rawRssi + Math.Min(packetSnrDb, 0));
        var hasPayloadCrc = (this.transport.ReadByte((byte)Rfm9xRegister.HopChannel) & Rfm9xBits.HopChannelPayloadCrc) != 0;
        var isCrcValid = (irqFlags & Rfm9xBits.IrqPayloadCrcError) == 0 && (!this.configuration.PayloadCrcEnabled || hasPayloadCrc);

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
        
        var lowFrequencyBit = this.configuration.FrequencyHertz < 525_000_000 ? Rfm9xBits.LowFrequencyMode : (byte)0;
        this.transport.WriteByte((byte)Rfm9xRegister.OpMode, (byte)(Rfm9xBits.LongRangeMode | lowFrequencyBit | modeBits));
        this.mode = mode;
    }

    private async Task WaitForDutyCycleAsync(CancellationToken cancellationToken)
    {
        var now = this.timeProvider.GetUtcNow();
        if (this.nextTransmitAt > now)
        {
            await Task.Delay(this.nextTransmitAt - now, this.timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private void EnsureConfigured()
    {
        if (!this.configured)
        {
            throw new InvalidOperationException("ConfigureAsync must complete before radio operation.");
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
    }
}