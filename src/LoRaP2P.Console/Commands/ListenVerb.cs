using System.Text;
using System.Text.Json;
using CommandLine;
using LoRaP2P.Protocol;

namespace LoRaP2P.Console.Commands;

[Verb("listen", HelpText = "Continuously receive and acknowledge P2P frames.")]
internal sealed class ListenVerb : IConsoleVerb
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        await context.Output.WriteLineAsync("WARNING: P2P payloads are not authenticated or encrypted.");
        await context.Output.WriteLineAsync(
            $"Listening as node {context.P2p.Node.LocalNodeId}. Press Ctrl+C to exit.");

        void WriteMessage(P2pReceivedMessage message)
        {
            var payload = Convert.ToHexString(message.Payload);
            string? text = null;
            try
            {
                text = StrictUtf8.GetString(message.Payload);
            }
            catch (DecoderFallbackException)
            {
            }

            var textOutput = text is null ? string.Empty : $" | text: {JsonSerializer.Serialize(text)}";
            context.Output.WriteLine(
                $"P2P Rx from {message.Sender}: sequence {message.SequenceNumber}, "
                + $"{message.Payload.Length} bytes, hex: {payload}{textOutput} | "
                + $"RSSI {message.RssiDbm} dBm | SNR {message.SnrDb:F2} dB");
        }

        context.P2p.Node.DataReceived += WriteMessage;
        try
        {
            await context.P2p.Node.ListenAsync(cancellationToken);
        }
        finally
        {
            context.P2p.Node.DataReceived -= WriteMessage;
        }

        return CommandOutcome.Continue;
    }
}
