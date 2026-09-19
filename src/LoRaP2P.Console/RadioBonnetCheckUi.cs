using System.Device.Gpio;
using System.Device.I2c;

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

internal sealed class SystemRadioBonnetCheckUi : IRadioBonnetCheckUi, IDisposable
{
    private const int DisplayResetPin = 4;
    private const int ButtonAPin = 5;
    private const int ButtonBPin = 6;
    private const int ButtonCPin = 12;
    private const int DisplayWidth = 128;
    private const int DisplayHeight = 32;
    private const int I2cBusId = 1;
    private const int DisplayAddress = 0x3C;

    private static readonly Dictionary<char, byte[]> Font = new()
    {
        [' '] = [0x00, 0x00, 0x00, 0x00, 0x00],
        [':'] = [0x00, 0x36, 0x36, 0x00, 0x00],
        ['9'] = [0x06, 0x49, 0x49, 0x29, 0x1E],
        ['A'] = [0x7E, 0x11, 0x11, 0x11, 0x7E],
        ['D'] = [0x7F, 0x41, 0x41, 0x22, 0x1C],
        ['E'] = [0x7F, 0x49, 0x49, 0x49, 0x41],
        ['F'] = [0x7F, 0x09, 0x09, 0x09, 0x01],
        ['M'] = [0x7F, 0x02, 0x0C, 0x02, 0x7F],
        ['O'] = [0x3E, 0x41, 0x41, 0x41, 0x3E],
        ['R'] = [0x7F, 0x09, 0x19, 0x29, 0x46],
        ['a'] = [0x20, 0x54, 0x54, 0x54, 0x78],
        ['c'] = [0x38, 0x44, 0x44, 0x44, 0x20],
        ['d'] = [0x38, 0x44, 0x44, 0x48, 0x7F],
        ['e'] = [0x38, 0x54, 0x54, 0x54, 0x18],
        ['i'] = [0x00, 0x44, 0x7D, 0x40, 0x00],
        ['o'] = [0x38, 0x44, 0x44, 0x44, 0x38],
        ['r'] = [0x7C, 0x08, 0x04, 0x04, 0x08],
        ['t'] = [0x04, 0x3F, 0x44, 0x40, 0x20],
        ['u'] = [0x3C, 0x40, 0x40, 0x20, 0x7C],
        ['x'] = [0x44, 0x28, 0x10, 0x28, 0x44]
    };

    private readonly GpioController _gpioController;
    private readonly I2cDevice _display;
    private readonly byte[] _frameBuffer = new byte[DisplayWidth * DisplayHeight / 8];
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            WriteCommands(0xAE);
        }
        finally
        {
            _display.Dispose();
            _gpioController.Dispose();
        }
    }

    private void Render(RadioBonnetButtons buttons)
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

        WriteCommands(0x21, 0x00, 0x7F);
        WriteCommands(0x22, 0x00, 0x03);

        for (var offset = 0; offset < _frameBuffer.Length; offset += 16)
        {
            WriteData(_frameBuffer.AsSpan(offset, Math.Min(16, _frameBuffer.Length - offset)));
        }
    }

    private void DrawText(int x, int y, string text)
    {
        var pageOffset = (y / 8) * DisplayWidth;
        foreach (var character in text)
        {
            if (!Font.TryGetValue(character, out var glyph) || x + glyph.Length > DisplayWidth)
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
        Render(RadioBonnetButtons.None);
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
