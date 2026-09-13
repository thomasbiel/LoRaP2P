using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("reset", HelpText = "Reset and reconfigure the radio.")]
internal sealed class ResetVerb : IConsoleVerb
{
    public async Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        await context.Radio.ResetAsync(cancellationToken);
        await context.Radio.ProbeAsync(cancellationToken);
        await context.Radio.ConfigureAsync(context.Configuration, cancellationToken);
        await context.Output.WriteLineAsync("Radio reset and configured.");
        return CommandOutcome.Continue;
    }
}