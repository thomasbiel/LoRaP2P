using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console;

internal static class RadioHardwareCheck
{
    public static async Task RunAsync(
        ILoraRadio radio,
        RadioConfiguration configuration,
        IRadioBonnetCheckUi bonnet,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        await output.WriteLineAsync("Checking RFM9x hardware...");

        try
        {
            await radio.ResetAsync(cancellationToken);
            await output.WriteLineAsync("Reset sequence: OK");

            var version = await radio.ProbeAsync(cancellationToken);
            await output.WriteLineAsync($"SPI/CE1 communication: OK (SX1276 version 0x{version:X2})");

            await radio.ConfigureAsync(configuration, cancellationToken);
            var status = await radio.GetStatusAsync(cancellationToken);
            if (status.ChipVersion != version)
            {
                throw new Rfm9xException($"Version register changed during hardware check: 0x{version:X2} to 0x{status.ChipVersion:X2}.");
            }

            if (status.Mode != RadioMode.Standby)
            {
                throw new Rfm9xException($"Radio did not enter standby after configuration. Current mode: {status.Mode}.");
            }

            await output.WriteLineAsync(
                $"Configuration: OK ({configuration.FrequencyHertz / 1_000_000:F3} MHz, "
                + $"SF{(int)configuration.SpreadingFactor}, "
                + $"BW{(int)configuration.Bandwidth / 1_000}, "
                + $"{configuration.OutputPowerDbm} dBm)");
                
            await output.WriteLineAsync($"Standby status: OK (IRQ 0x{status.IrqFlags:X2})");
            bonnet.ShowRadioStatus(detected: true);
        }
        catch (Exception exception) when (exception is IOException or Rfm9xException or TimeoutException)
        {
            bonnet.ShowRadioStatus(detected: false);
            throw;
        }

        await output.WriteLineAsync("RFM9x hardware check passed. Press buttons A, B, and C; press Ctrl+C to exit.");
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bonnet.ShowButtons(bonnet.ReadButtons());
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }
}
