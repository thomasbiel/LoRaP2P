using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("quit", HelpText = "Exit the application.")]
internal sealed class QuitVerb : IConsoleVerb
{
    public Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        return Task.FromResult(CommandOutcome.Exit);
    }
}