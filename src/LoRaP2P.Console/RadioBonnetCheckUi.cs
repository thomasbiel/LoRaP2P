using System.Device.Gpio;
using System.Device.I2c;
using System.Runtime.ExceptionServices;

namespace LoRaP2P.Console;

[Flags]
internal enum RadioBonnetButtons
{
    None = 0,
    A = 1,
    B = 2,
    C = 4
}

internal interface IRadioBonnetCheckUi
{
    void ShowRadioStatus(bool detected);

    RadioBonnetButtons ReadButtons();

    void ShowButtons(RadioBonnetButtons buttons);
}

internal interface IRadioBonnetDisplay
{
    void ShowBroadcastMessage(string message);
}

internal sealed class SystemRadioBonnetCheckUi : IRadioBonnetCheckUi, IRadioBonnetDisplay, IDisposable
{
    private const int DisplayResetPin = 4;
    private const int ButtonAPin = 5;
    private const int ButtonBPin = 6;
    private const int ButtonCPin = 12;
    private const int DisplayWidth = 128;
    private const int DisplayHeight = 32;
    private const int I2cBusId = 1;
    private const int DisplayAddress = 0x3C;
    private static readonly TimeSpan ScrollInterval = TimeSpan.FromSeconds(1);

    private readonly GpioController _gpioController;
    private readonly I2cDevice _display;
    private readonly byte[] _frameBuffer = new byte[DisplayWidth * DisplayHeight / 8];
    private readonly Lock _displayLock = new();
    private readonly Lock _scrollLock = new();
    private CancellationTokenSource? _scrollCancellation;
    private Task _scrollTask = Task.CompletedTask;
    private bool _radioDetected;
    private bool _disposed;

    public SystemRadioBonnetCheckUi()
    {
        _gpioController = new GpioController();
        I2cDevice? i2cDevice = null;

        try
        {
            _gpioController.OpenPin(DisplayResetPin, PinMode.Output);
            _gpioController.Write(DisplayResetPin, PinValue.Low);
            Thread.Sleep(10);
            _gpioController.Write(DisplayResetPin, PinValue.High);
            Thread.Sleep(10);

            _gpioController.OpenPin(ButtonAPin, PinMode.InputPullUp);
            _gpioController.OpenPin(ButtonBPin, PinMode.InputPullUp);
            _gpioController.OpenPin(ButtonCPin, PinMode.InputPullUp);

            i2cDevice = I2cDevice.Create(new I2cConnectionSettings(I2cBusId, DisplayAddress));
            _display = i2cDevice;
            InitializeDisplay();
        }
        catch
        {
            i2cDevice?.Dispose();
            _gpioController.Dispose();
            throw;
        }
    }

    public void ShowRadioStatus(bool detected)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _radioDetected = detected;
        Render(RadioBonnetButtons.None);
    }

    public RadioBonnetButtons ReadButtons()
    {
        var buttons = RadioBonnetButtons.None;
        if (_gpioController.Read(ButtonAPin) == PinValue.Low)
        {
            buttons |= RadioBonnetButtons.A;
        }

        if (_gpioController.Read(ButtonBPin) == PinValue.Low)
        {
            buttons |= RadioBonnetButtons.B;
        }

        if (_gpioController.Read(ButtonCPin) == PinValue.Low)
        {
            buttons |= RadioBonnetButtons.C;
        }

        return buttons;
    }

    public void ShowButtons(RadioBonnetButtons buttons) => Render(buttons);

    public void ShowBroadcastMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var lines = RadioBonnetTextLayout.Wrap(message);

        lock (_scrollLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_scrollTask.IsFaulted)
            {
                _scrollTask.GetAwaiter().GetResult();
            }

            var previousTask = _scrollTask;
            var previousCancellation = _scrollCancellation;
            previousCancellation?.Cancel();
            _scrollCancellation = new CancellationTokenSource();
            _scrollTask = ScrollBroadcastAsync(
                previousTask,
                previousCancellation,
                lines,
                _scrollCancellation.Token);
        }
    }

    public void Dispose()
    {
        Task scrollTask;
        CancellationTokenSource? scrollCancellation;
        lock (_scrollLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            scrollCancellation = _scrollCancellation;
            scrollCancellation?.Cancel();
            scrollTask = _scrollTask;
        }

        Exception? scrollException = null;
        try
        {
            scrollTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException) when (scrollCancellation?.IsCancellationRequested == true)
        {
        }
        catch (Exception exception)
        {
            scrollException = exception;
        }
        finally
        {
            scrollCancellation?.Dispose();
        }

        try
        {
            lock (_displayLock)
            {
                WriteCommands(0xAE);
            }
        }
        finally
        {
            _display.Dispose();
            _gpioController.Dispose();
        }

        if (scrollException is not null)
        {
            ExceptionDispatchInfo.Capture(scrollException).Throw();
        }
    }

    private void Render(RadioBonnetButtons buttons)
    {
        lock (_displayLock)
        {
            Array.Clear(_frameBuffer);
            DrawText(0, 0, _radioDetected ? "RFM9x: Detected" : "RFM9x: ERROR");

            if ((buttons & RadioBonnetButtons.A) != 0)
            {
                DrawText(0, 24, "Ada");
            }

            if ((buttons & RadioBonnetButtons.B) != 0)
            {
                DrawText(42, 24, "Fruit");
            }

            if ((buttons & RadioBonnetButtons.C) != 0)
            {
                DrawText(84, 24, "Radio");
            }

            FlushFrameBuffer();
        }
    }

    private async Task ScrollBroadcastAsync(
        Task previousTask,
        CancellationTokenSource? previousCancellation,
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await previousTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (previousCancellation?.IsCancellationRequested == true)
            {
            }
            finally
            {
                previousCancellation?.Dispose();
            }

            var lastOffset = Math.Max(0, lines.Count - RadioBonnetTextLayout.VisibleLines);
            for (var lineOffset = 0; lineOffset <= lastOffset; lineOffset++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RenderBroadcast(lines, lineOffset);
                if (lineOffset < lastOffset)
                {
                    await Task.Delay(ScrollInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void RenderBroadcast(IReadOnlyList<string> lines, int lineOffset)
    {
        lock (_displayLock)
        {
            Array.Clear(_frameBuffer);
            var visibleLineCount = Math.Min(
                RadioBonnetTextLayout.VisibleLines,
                lines.Count - lineOffset);
            for (var line = 0; line < visibleLineCount; line++)
            {
                DrawText(0, line * 8, lines[lineOffset + line]);
            }

            FlushFrameBuffer();
        }
    }

    private void DrawText(int x, int y, string text)
    {
        var pageOffset = (y / 8) * DisplayWidth;
        foreach (var character in text)
        {
            var glyph = RadioBonnetFont.Glyphs.GetValueOrDefault(character)
                ?? RadioBonnetFont.Glyphs['?'];
            if (x + glyph.Length > DisplayWidth)
            {
                break;
            }

            glyph.CopyTo(_frameBuffer, pageOffset + x);
            x += glyph.Length + 1;
        }
    }

    private void InitializeDisplay()
    {
        WriteCommands(0xAE);
        WriteCommands(0xD5, 0x80);
        WriteCommands(0xA8, 0x1F);
        WriteCommands(0xD3, 0x00);
        WriteCommands(0x40);
        WriteCommands(0x8D, 0x14);
        WriteCommands(0x20, 0x00);
        WriteCommands(0xA1);
        WriteCommands(0xC8);
        WriteCommands(0xDA, 0x02);
        WriteCommands(0x81, 0x8F);
        WriteCommands(0xD9, 0xF1);
        WriteCommands(0xDB, 0x40);
        WriteCommands(0xA4);
        WriteCommands(0xA6);
        WriteCommands(0xAF);
        lock (_displayLock)
        {
            Array.Clear(_frameBuffer);
            FlushFrameBuffer();
        }
    }

    private void FlushFrameBuffer()
    {
        WriteCommands(0x21, 0x00, 0x7F);
        WriteCommands(0x22, 0x00, 0x03);

        for (var offset = 0; offset < _frameBuffer.Length; offset += 16)
        {
            WriteData(_frameBuffer.AsSpan(offset, Math.Min(16, _frameBuffer.Length - offset)));
        }
    }

    private void WriteCommands(params byte[] commands)
    {
        var buffer = new byte[commands.Length + 1];
        buffer[0] = 0x00;
        commands.CopyTo(buffer, 1);
        _display.Write(buffer);
    }

    private void WriteData(ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[data.Length + 1];
        buffer[0] = 0x40;
        data.CopyTo(buffer[1..]);
        _display.Write(buffer);
    }
}
