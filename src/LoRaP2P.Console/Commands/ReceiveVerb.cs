using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("receive", HelpText = "Wait for one packet.")]
internal sealed class ReceiveVerb : IConsoleVerb
{
    [Value(0, MetaName = "timeout-seconds", Required = false, HelpText = "Receive timeout in seconds")]
    public double? TimeoutSeconds { get; set; }

    public async Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(this.TimeoutSeconds ?? 10);
        var packet = await context.Radio.ReceiveSingleAsync(timeout, cancellationToken);
        if (packet is null)
        {
            await context.Output.WriteLineAsync("Receive timeout.");
            return CommandOutcome.Continue;
        }

        await context.Output.WriteLineAsync(
            $"RxDone: {Convert.ToHexString(packet.Payload)} | CRC {(packet.IsCrcValid ? "valid" : "invalid")} | "
            + $"RSSI {packet.PacketRssiDbm} dBm | SNR {packet.PacketSnrDb:F2} dB");
        
        return CommandOutcome.Continue;
    }
}