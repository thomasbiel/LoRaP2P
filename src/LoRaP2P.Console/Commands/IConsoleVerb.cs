namespace LoRaP2P.ConsoleApp.Commands;

internal interface IConsoleVerb
{
    Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken);
}
