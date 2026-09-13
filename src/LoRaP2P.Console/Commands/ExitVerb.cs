using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("exit", HelpText = "Exit the application.")]
internal sealed class ExitVerb : IConsoleVerb
{
    public Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommandOutcome.Exit);
    }
}