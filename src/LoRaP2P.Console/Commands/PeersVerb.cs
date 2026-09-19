using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("peers", HelpText = "Show configured peers and recent reception data.")]
internal sealed class PeersVerb : IConsoleVerb
{
    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        await context.Output.WriteLineAsync($"Local node: {context.P2p.Node.LocalNodeId}");
        var peers = context.P2p.Node.Peers.GetSnapshot();
        if (peers.Count == 0)
        {
            await context.Output.WriteLineAsync("No peers registered.");
            return CommandOutcome.Continue;
        }

        foreach (var peer in peers)
        {
            var reception = peer.LastReceivedAt is null
                ? "never received"
                : $"last {peer.LastReceivedAt:O}, sequence {peer.LastSequenceNumber}, "
                  + $"RSSI {peer.LastRssiDbm} dBm, SNR {peer.LastSnrDb:F2} dB";
            await context.Output.WriteLineAsync($"Peer {peer.NodeId}: {reception}");
        }

        return CommandOutcome.Continue;
    }
}
