using System.Device.Gpio;
using System.Device.I2c;

namespace LoRaP2P.Display.RadioBonnet;

public sealed class RadioBonnetDisplay : IDisposable
{
    private const int ResetPin = 4;
    private readonly GpioController gpio;
    private readonly I2cDevice display;
    private readonly RadioBonnetFrameBuffer frame = new();
    private readonly Lock sync = new();
    private bool disposed;

    public RadioBonnetDisplay()
    {
        this.gpio = new GpioController();
        I2cDevice? display = null;
        try
        {
            this.gpio.OpenPin(ResetPin, PinMode.Output);
            this.gpio.Write(ResetPin, PinValue.Low);
            Thread.Sleep(10);
            this.gpio.Write(ResetPin, PinValue.High);
            Thread.Sleep(10);
            display = I2cDevice.Create(new I2cConnectionSettings(1, 0x3C));
            this.display = display;
            this.WriteCommands(0xAE, 0xD5, 0x80, 0xA8, 0x1F, 0xD3, 0x00, 0x40,
                0x8D, 0x14, 0x20, 0x00, 0xA1, 0xC8, 0xDA, 0x02, 0x81, 0x8F,
                0xD9, 0xF1, 0xDB, 0x40, 0xA4, 0xA6);
            this.ShowLines([]);
            this.WriteCommands(0xAF);
        }
        catch
        {
            display?.Dispose();
            this.gpio.Dispose();
            throw;
        }
    }

    public void ShowLines(IReadOnlyList<string> lines)
    {
        lock (this.sync)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            this.frame.Render(lines);
            this.WriteCommands(0x21, 0x00, 0x7F, 0x22, 0x00, 0x03);
            Span<byte> packet = stackalloc byte[17];
            packet[0] = 0x40;
            for (var offset = 0; offset < this.frame.Pixels.Length; offset += 16)
            {
                this.frame.Pixels.AsSpan(offset, 16).CopyTo(packet[1..]);
                this.display.Write(packet);
            }
        }
    }

    public void Dispose()
    {
        lock (this.sync)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            try
            {
                this.WriteCommands(0xAE);
            }
            finally
            {
                this.display.Dispose();
                this.gpio.Dispose();
            }
        }
    }

    private void WriteCommands(params byte[] commands)
    {
        var packet = new byte[commands.Length + 1];
        commands.CopyTo(packet, 1);
        this.display.Write(packet);
    }
}