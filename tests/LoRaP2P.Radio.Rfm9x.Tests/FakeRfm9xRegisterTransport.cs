using LoRaP2P.Radio.Rfm9x.Transport;

namespace LoRaP2P.Radio.Rfm9x.Tests;

internal sealed class FakeRfm9xRegisterTransport : IRfm9xRegisterTransport
{
    private readonly Lock sync = new();
    private readonly Dictionary<byte, byte> registers = new();
    private TaskCompletionSource dio0Signal = CreateSignal();
    private byte[] fifoReadData = [];

    public FakeRfm9xRegisterTransport()
    {
        this.registers[0x42] = 0x12;
    }

    public IReadOnlyList<bool> ResetStates => this.resetStates;

    public byte[] LastFifoWrite { get; private set; } = [];

    private readonly List<bool> resetStates = [];

    public byte ReadByte(byte address)
    {
        lock (this.sync)
        {
            return this.registers.GetValueOrDefault(address);
        }
    }

    public void Read(byte address, Span<byte> destination)
    {
        lock (this.sync)
        {
            if (address == 0x00)
            {
                this.fifoReadData.AsSpan(0, Math.Min(destination.Length, this.fifoReadData.Length)).CopyTo(destination);
                return;
            }

            for (var offset = 0; offset < destination.Length; offset++)
            {
                destination[offset] = this.registers.GetValueOrDefault(checked((byte)(address + offset)));
            }
        }
    }

    public void WriteByte(byte address, byte value)
    {
        lock (this.sync)
        {
            this.registers[address] = address == 0x12 ? (byte)0 : value;
        }
    }

    public void Write(byte address, ReadOnlySpan<byte> source)
    {
        lock (this.sync)
        {
            if (address == 0x00)
            {
                this.LastFifoWrite = [.. source];
                return;
            }

            for (var offset = 0; offset < source.Length; offset++)
            {
                this.registers[checked((byte)(address + offset))] = source[offset];
            }
        }
    }

    public void SetReset(bool asserted)
    {
        lock (this.sync)
        {
            this.resetStates.Add(asserted);
        }
    }

    public void ClearDio0Signal()
    {
        lock (this.sync)
        {
            this.dio0Signal = CreateSignal();
        }
    }

    public ValueTask WaitForDio0RisingEdgeAsync(CancellationToken cancellationToken)
    {
        Task signalTask;
        lock (this.sync)
        {
            signalTask = this.dio0Signal.Task;
        }

        return new ValueTask(signalTask.WaitAsync(cancellationToken));
    }

    public void SetRegister(byte address, byte value)
    {
        lock (this.sync)
        {
            this.registers[address] = value;
        }
    }

    public void SetFifoReadData(ReadOnlySpan<byte> payload)
    {
        lock (this.sync)
        {
            this.fifoReadData = [.. payload];
        }
    }

    public void SignalDio0()
    {
        TaskCompletionSource signal;
        lock (this.sync)
        {
            signal = this.dio0Signal;
        }

        signal.TrySetResult();
    }

    public void Dispose() { }

    private static TaskCompletionSource CreateSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}