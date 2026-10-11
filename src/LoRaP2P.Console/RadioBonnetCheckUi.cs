using System.Device.Gpio;
using System.Runtime.ExceptionServices;
using LoRaP2P.Display.RadioBonnet;

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
    private const int ButtonAPin = 5;
    private const int ButtonBPin = 6;
    private const int ButtonCPin = 12;
    private static readonly TimeSpan ScrollInterval = TimeSpan.FromSeconds(1);

    private readonly GpioController gpioController;
    private readonly RadioBonnetDisplay display;
    private readonly Lock displayLock = new();
    private readonly Lock scrollLock = new();
    private CancellationTokenSource? scrollCancellation;
    private Task scrollTask = Task.CompletedTask;
    private bool radioDetected;
    private bool disposed;

    public SystemRadioBonnetCheckUi()
    {
        this.gpioController = new GpioController();
        RadioBonnetDisplay? display = null;

        try
        {
            this.gpioController.OpenPin(ButtonAPin, PinMode.InputPullUp);
            this.gpioController.OpenPin(ButtonBPin, PinMode.InputPullUp);
            this.gpioController.OpenPin(ButtonCPin, PinMode.InputPullUp);

            display = new RadioBonnetDisplay();
            this.display = display;
        }
        catch
        {
            display?.Dispose();
            this.gpioController.Dispose();
            throw;
        }
    }

    public void ShowRadioStatus(bool detected)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        this.radioDetected = detected;
        this.Render(RadioBonnetButtons.None);
    }

    public RadioBonnetButtons ReadButtons()
    {
        var buttons = RadioBonnetButtons.None;
        if (this.gpioController.Read(ButtonAPin) == PinValue.Low)
        {
            buttons |= RadioBonnetButtons.A;
        }

        if (this.gpioController.Read(ButtonBPin) == PinValue.Low)
        {
            buttons |= RadioBonnetButtons.B;
        }

        if (this.gpioController.Read(ButtonCPin) == PinValue.Low)
        {
            buttons |= RadioBonnetButtons.C;
        }

        return buttons;
    }

    public void ShowButtons(RadioBonnetButtons buttons) => this.Render(buttons);

    public void ShowBroadcastMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var lines = RadioBonnetTextLayout.Wrap(message);

        lock (this.scrollLock)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.scrollTask.IsFaulted)
            {
                this.scrollTask.GetAwaiter().GetResult();
            }

            var previousTask = this.scrollTask;
            var previousCancellation = this.scrollCancellation;
            previousCancellation?.Cancel();
            this.scrollCancellation = new CancellationTokenSource();
            this.scrollTask = this.ScrollBroadcastAsync(
                previousTask,
                previousCancellation,
                lines,
                this.scrollCancellation.Token);
        }
    }

    public void Dispose()
    {
        Task scrollTask;
        CancellationTokenSource? scrollCancellation;
        lock (this.scrollLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            scrollCancellation = this.scrollCancellation;
            scrollCancellation?.Cancel();
            scrollTask = this.scrollTask;
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
            lock (this.displayLock)
            {
                this.display.Dispose();
            }
        }
        finally
        {
            this.gpioController.Dispose();
        }

        if (scrollException is not null)
        {
            ExceptionDispatchInfo.Capture(scrollException).Throw();
        }
    }

    private void Render(RadioBonnetButtons buttons)
    {
        lock (this.displayLock)
        {
            var buttonA = (buttons & RadioBonnetButtons.A) != 0 ? "Ada    " : "       ";
            var buttonB = (buttons & RadioBonnetButtons.B) != 0 ? "Fruit  " : "       ";
            var buttonC = (buttons & RadioBonnetButtons.C) != 0 ? "Radio" : "";
            this.display.ShowLines([this.radioDetected ? "RFM9x: Detected" : "RFM9x: ERROR",
                "", "", buttonA + buttonB + buttonC]);
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
                this.RenderBroadcast(lines, lineOffset);
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
        lock (this.displayLock)
        {
            var visibleLineCount = Math.Min(
                RadioBonnetTextLayout.VisibleLines,
                lines.Count - lineOffset);
            var visible = new string[visibleLineCount];
            for (var line = 0; line < visibleLineCount; line++)
            {
                visible[line] = lines[lineOffset + line];
            }

            this.display.ShowLines(visible);
        }
    }
}
