using CommandLine;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.ConsoleApp.Commands;

[Verb("receive", HelpText = "Wait for one packet.")]
internal sealed class ReceiveVerb : IConsoleVerb
{
    [Value(0, MetaName = "timeout-seconds", Required = false, HelpText = "Receive timeout in seconds")]
    public double? TimeoutSeconds { get; set; }

    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        TimeSpan timeout = TimeSpan.FromSeconds(TimeoutSeconds ?? 10);
        ReceivedPacket? packet = await context.Radio.ReceiveSingleAsync(timeout, cancellationToken);
        if (packet is null)
        {
            context.Output.WriteLine("Receive timeout.");
            return CommandOutcome.Continue;
        }

        context.Output.WriteLine(
            $"RxDone: {Convert.ToHexString(packet.Payload)} | CRC {(packet.IsCrcValid ? "valid" : "invalid")} | "
            + $"RSSI {packet.PacketRssiDbm} dBm | SNR {packet.PacketSnrDb:F2} dB");
        return CommandOutcome.Continue;
    }
}