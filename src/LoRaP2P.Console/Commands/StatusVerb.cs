using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("status", HelpText = "Show mode and radio profile.")]
internal sealed class StatusVerb : IConsoleVerb
{
    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var status = await context.Radio.GetStatusAsync(cancellationToken);
        var configuration = status.Configuration;
        await context.Output.WriteLineAsync($"Chip: 0x{status.ChipVersion:X2}; mode: {status.Mode}; IRQ: 0x{status.IrqFlags:X2}");
        
        await context.Output.WriteLineAsync(
            $"Frequency: {configuration.FrequencyHertz / 1_000_000:F3} MHz; "
            + $"SF: {(int)configuration.SpreadingFactor}; "
            + $"BW: {(int)configuration.Bandwidth / 1_000} kHz; "
            + $"power: {configuration.OutputPowerDbm} dBm");
        
        return CommandOutcome.Continue;
    }
}