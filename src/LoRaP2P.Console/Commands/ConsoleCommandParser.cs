using System.Globalization;
using CommandLine;

namespace LoRaP2P.ConsoleApp.Commands;

internal sealed class ConsoleCommandParser : IDisposable
{
    private static readonly Type[] VerbTypes =
    [
        typeof(ConfigureVerb),
        typeof(ExitVerb),
        typeof(HelpVerb),
        typeof(ProbeVerb),
        typeof(QuitVerb),
        typeof(ReceiveVerb),
        typeof(RegisterVerb),
        typeof(ResetVerb),
        typeof(SendVerb),
        typeof(StatusVerb),
    ];

    private readonly Parser _parser;

    public ConsoleCommandParser(TextWriter error)
    {
        _parser = new Parser(settings =>
        {
            settings.AutoHelp = false;
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
        string[] arguments = CommandLineTokenizer.Tokenize(commandLine);
        ParserResult<object> result = _parser.ParseArguments(arguments, VerbTypes);
        return await result.MapResult(
            verb => ((IConsoleVerb)verb).ExecuteAsync(context, cancellationToken),
            _ => Task.FromResult(CommandOutcome.Continue));
    }

    public void Dispose() => _parser.Dispose();
}
