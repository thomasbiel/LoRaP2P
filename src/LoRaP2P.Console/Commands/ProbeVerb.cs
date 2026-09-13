using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("probe", HelpText = "Read and verify chip version.")]
internal sealed class ProbeVerb : IConsoleVerb
{
    public async Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var version = await context.Radio.ProbeAsync(cancellationToken);
        await context.Output.WriteLineAsync($"SX1276 version: 0x{version:X2}");
        return CommandOutcome.Continue;
    }
}