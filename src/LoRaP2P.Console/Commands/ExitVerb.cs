using CommandLine;

namespace LoRaP2P.ConsoleApp.Commands;

[Verb("exit", HelpText = "Exit the application.")]
internal sealed class ExitVerb : IConsoleVerb
{
    public Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(CommandOutcome.Exit);
}