using CommandLine;

namespace LoRaP2P.ConsoleApp.Commands;

[Verb("probe", HelpText = "Read and verify chip version.")]
internal sealed class ProbeVerb : IConsoleVerb
{
    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        byte version = await context.Radio.ProbeAsync(cancellationToken);
        context.Output.WriteLine($"SX1276 version: 0x{version:X2}");
        return CommandOutcome.Continue;
    }
}