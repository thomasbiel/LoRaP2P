using LoRaP2P.Radio.Rfm9x.Transport;

namespace LoRaP2P.Radio.Rfm9x.Tests;

internal sealed class FakeRfm9xRegisterTransport : IRfm9xRegisterTransport
{
    private readonly object _sync = new();
    private readonly Dictionary<byte, byte> _registers = new();
    private TaskCompletionSource _dio0Signal = CreateSignal();
    private byte[] _fifoReadData = [];

    public FakeRfm9xRegisterTransport()
    {
        _registers[0x42] = 0x12;
    }

    public IReadOnlyList<bool> ResetStates => _resetStates;

    public byte[] LastFifoWrite { get; private set; } = [];

    private readonly List<bool> _resetStates = [];

    public byte ReadByte(byte address)
    {
        lock (_sync)
        {
            return _registers.GetValueOrDefault(address);
        }
    }

    public void Read(byte address, Span<byte> destination)
    {
        lock (_sync)
        {
            if (address == 0x00)
            {
                _fifoReadData.AsSpan(0, Math.Min(destination.Length, _fifoReadData.Length)).CopyTo(destination);
                return;
            }

            for (int offset = 0; offset < destination.Length; offset++)
            {
                destination[offset] = _registers.GetValueOrDefault(checked((byte)(address + offset)));
            }
        }
    }

    public void WriteByte(byte address, byte value)
    {
        lock (_sync)
        {
            _registers[address] = address == 0x12 ? (byte)0 : value;
        }
    }

    public void Write(byte address, ReadOnlySpan<byte> source)
    {
        lock (_sync)
        {
            if (address == 0x00)
            {
                LastFifoWrite = source.ToArray();
                return;
            }

            for (int offset = 0; offset < source.Length; offset++)
            {
                _registers[checked((byte)(address + offset))] = source[offset];
            }
        }
    }

    public void SetReset(bool asserted)
    {
        lock (_sync)
        {
            _resetStates.Add(asserted);
        }
    }

    public void ClearDio0Signal()
    {
        lock (_sync)
        {
            _dio0Signal = CreateSignal();
        }
    }

    public ValueTask WaitForDio0RisingEdgeAsync(CancellationToken cancellationToken)
    {
        Task signalTask;
        lock (_sync)
        {
            signalTask = _dio0Signal.Task;
        }

        return new ValueTask(signalTask.WaitAsync(cancellationToken));
    }

    public void SetRegister(byte address, byte value)
    {
        lock (_sync)
        {
            _registers[address] = value;
        }
    }

    public void SetFifoReadData(ReadOnlySpan<byte> payload)
    {
        lock (_sync)
        {
            _fifoReadData = payload.ToArray();
        }
    }

    public void SignalDio0()
    {
        TaskCompletionSource signal;
        lock (_sync)
        {
            signal = _dio0Signal;
        }

        signal.TrySetResult();
    }

    public void Dispose()
    {
    }

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}