using System.Globalization;
using CommandLine;
using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Console.Commands;

[Verb("peer", HelpText = "Configure the local node ID or register a peer.")]
internal sealed class PeerVerb : IConsoleVerb
{
    [Value(0, MetaName = "operation", Required = true, HelpText = "id or add")]
    public string Operation { get; set; } = string.Empty;

    [Value(1, MetaName = "id", Required = true, HelpText = "Node ID from 1 through 65534")]
    public string NodeId { get; set; } = string.Empty;

    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var nodeId = ParseNodeId(this.NodeId);
        switch (this.Operation.ToLowerInvariant())
        {
            case "id":
                await context.P2p.SetLocalNodeIdAsync(nodeId, cancellationToken);
                await context.Output.WriteLineAsync($"Local node ID set to {nodeId}.");
                break;
            case "add":
                var added = await context.P2p.AddPeerAsync(nodeId, cancellationToken);
                await context.Output.WriteLineAsync(
                    added ? $"Peer {nodeId} registered." : $"Peer {nodeId} is already registered.");
                break;
            default:
                throw new FormatException("Usage: peer id <id> or peer add <id>");
        }

        return CommandOutcome.Continue;
    }

    private static P2pNodeId ParseNodeId(string value) =>
        ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? new P2pNodeId(result)
            : throw new FormatException($"Invalid node ID: '{value}'.");
}
