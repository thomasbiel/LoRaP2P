using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("stats", HelpText = "Show P2P session statistics.")]
internal sealed class StatsVerb : IConsoleVerb
{
    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        var stats = context.P2p.Node.Statistics.GetSnapshot();
        await context.Output.WriteLineAsync(
            $"P2P stats: TX {stats.DataFramesSent}, RX {stats.DataFramesReceived}, "
            + $"ACK sent {stats.AcksSent}, ACK received {stats.AcksReceived}, "
            + $"retries {stats.Retries}, timeouts {stats.AckTimeouts}, "
            + $"duplicates {stats.Duplicates}, CRC errors {stats.CrcErrors}, "
            + $"invalid {stats.InvalidFramesDropped}, foreign {stats.ForeignFramesDropped}, "
            + $"delivery failures {stats.DeliveryFailures}.");
        return CommandOutcome.Continue;
    }
}
