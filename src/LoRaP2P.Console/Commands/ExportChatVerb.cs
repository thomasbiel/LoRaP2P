using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("export-chat", HelpText = "Convert a JSONL P2P session to a self-contained HTML chat.")]
internal sealed class ExportChatVerb : IConsoleVerb
{
    [Value(0, MetaName = "session.jsonl", Required = true, HelpText = "Input session file")]
    public string InputPath { get; set; } = string.Empty;

    [Value(1, MetaName = "output.html", Required = false, HelpText = "Optional output HTML file")]
    public string? OutputPath { get; set; }

    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var outputPath = await P2pChatExporter.ExportAsync(
            this.InputPath,
            this.OutputPath,
            cancellationToken);
        await context.Output.WriteLineAsync($"Chat exported to {outputPath}");
        return CommandOutcome.Continue;
    }
}
