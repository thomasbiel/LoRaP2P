using System.Globalization;
using CommandLine;
using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Console.Commands;

[Verb("send-to", HelpText = "Send a P2P payload and wait for an acknowledgement.")]
internal sealed class SendToVerb : IConsoleVerb
{
    [Value(0, MetaName = "peer-id", Required = true, HelpText = "Destination node ID")]
    public string PeerId { get; set; } = string.Empty;

    [Value(1, MetaName = "format", Required = true, HelpText = "text or hex")]
    public string Format { get; set; } = string.Empty;

    [Value(2, MetaName = "value", Required = true, Min = 1, HelpText = "Payload value")]
    public IEnumerable<string> PayloadParts { get; set; } = [];

    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var peer = ParseNodeId(this.PeerId);
        if (!context.P2p.Node.Peers.Contains(peer))
        {
            throw new InvalidOperationException($"Peer {peer} is not registered.");
        }

        var payload = CommandPayloadParser.Parse(this.Format, this.PayloadParts);
        await context.Output.WriteLineAsync("WARNING: P2P payload is not authenticated or encrypted.");
        var result = await context.P2p.SendAsync(peer, payload, this.Format, cancellationToken);
        await context.Output.WriteLineAsync(
            $"ACK from peer {result.Peer}: sequence {result.SequenceNumber}, "
            + $"{result.Attempts} attempt(s), time-on-air {result.LastTimeOnAir.TotalMilliseconds:F1} ms, "
            + $"ACK latency {result.AckLatency.TotalMilliseconds:F1} ms.");
        return CommandOutcome.Continue;
    }

    private static P2pNodeId ParseNodeId(string value) =>
        ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? new P2pNodeId(result)
            : throw new FormatException($"Invalid peer ID: '{value}'.");
}
