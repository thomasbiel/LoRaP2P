using System.Globalization;
using CommandLine;

namespace LoRaP2P.Console.Commands;

internal sealed class ConsoleCommandParser : IDisposable
{
    private static readonly Type[] VerbTypes =
    [
        typeof(BroadcastVerb),
        typeof(ConfigureVerb),
        typeof(ExitVerb),
        typeof(ExportChatVerb),
        typeof(ListenVerb),
        typeof(PeersVerb),
        typeof(PeerVerb),
        typeof(ProbeVerb),
        typeof(QuitVerb),
        typeof(ReceiveVerb),
        typeof(RegisterVerb),
        typeof(ResetVerb),
        typeof(SendVerb),
        typeof(SendToVerb),
        typeof(StatsVerb),
        typeof(StatusVerb)
    ];

    private readonly Parser _parser;

    public ConsoleCommandParser(TextWriter error)
    {
        this._parser = new Parser(settings =>
        {
            settings.AutoHelp = true;
            settings.CaseSensitive = false;
            settings.CaseInsensitiveEnumValues = true;
            settings.HelpWriter = error;
            settings.ParsingCulture = CultureInfo.InvariantCulture;
        });
    }

    public async Task<CommandOutcome> ParseAndExecuteAsync(
        string commandLine,
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var arguments = CommandLineTokenizer.Tokenize(commandLine);
        var result = this._parser.ParseArguments(arguments, VerbTypes);
        return await result.MapResult<IConsoleVerb, Task<CommandOutcome>>(
            verb => verb.ExecuteAsync(context, cancellationToken),
            _ => Task.FromResult(CommandOutcome.Continue));
    }

    public void Dispose() => this._parser.Dispose();
}
