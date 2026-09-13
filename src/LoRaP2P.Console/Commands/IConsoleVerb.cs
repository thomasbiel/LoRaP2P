namespace LoRaP2P.Console.Commands;

internal interface IConsoleVerb
{
    Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken);
}
