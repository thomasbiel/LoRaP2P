using CommandLine;

namespace LoRaP2P.ConsoleApp.Commands;

[Verb("quit", HelpText = "Exit the application.")]
internal sealed class QuitVerb : IConsoleVerb
{
    public Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(CommandOutcome.Exit);
}