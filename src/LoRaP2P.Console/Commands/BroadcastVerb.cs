using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("broadcast", HelpText = "Broadcast a P2P text payload without acknowledgement.")]
internal sealed class BroadcastVerb : IConsoleVerb
{
    [Value(0, MetaName = "format", Required = true, HelpText = "text")]
    public string Format { get; set; } = string.Empty;

    [Value(1, MetaName = "value", Required = true, Min = 1, HelpText = "Payload value")]
    public IEnumerable<string> PayloadParts { get; set; } = [];

    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var payload = CommandPayloadParser.ParseText(this.Format, this.PayloadParts);
        await context.Output.WriteLineAsync("WARNING: P2P payload is not authenticated or encrypted.");
        var result = await context.P2p.BroadcastAsync(payload, this.Format, cancellationToken);
        await context.Output.WriteLineAsync(
            $"Broadcast TxDone: sequence {result.SequenceNumber}, {payload.Length} bytes, "
            + $"time-on-air {result.TimeOnAir.TotalMilliseconds:F1} ms; no ACK expected.");
        return CommandOutcome.Continue;
    }
}
