using CommandLine;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.ConsoleApp.Commands;

[Verb("status", HelpText = "Show mode and radio profile.")]
internal sealed class StatusVerb : IConsoleVerb
{
    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        RadioStatus status = await context.Radio.GetStatusAsync(cancellationToken);
        RadioConfiguration configuration = status.Configuration;
        context.Output.WriteLine(
            $"Chip: 0x{status.ChipVersion:X2}; mode: {status.Mode}; IRQ: 0x{status.IrqFlags:X2}");
        context.Output.WriteLine(
            $"Frequency: {configuration.FrequencyHertz / 1_000_000:F3} MHz; "
            + $"SF: {(int)configuration.SpreadingFactor}; "
            + $"BW: {(int)configuration.Bandwidth / 1_000} kHz; "
            + $"power: {configuration.OutputPowerDbm} dBm");
        return CommandOutcome.Continue;
    }
}