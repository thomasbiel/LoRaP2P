using System.Text;
using CommandLine;

namespace LoRaP2P.Console.Commands;

[Verb("send", HelpText = "Transmit a UTF-8 or hexadecimal payload.")]
internal sealed class SendVerb : IConsoleVerb
{
    [Value(0, MetaName = "format", Required = true, HelpText = "text or hex")]
    public string Format { get; set; } = string.Empty;

    [Value(1, MetaName = "value", Required = true, Min = 1, HelpText = "Payload value")]
    public IEnumerable<string> PayloadParts { get; set; } = [];

    public async Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var payload = this.Format.ToLowerInvariant() switch
        {
            "text" => Encoding.UTF8.GetBytes(string.Join(' ', this.PayloadParts)),
            "hex" => Convert.FromHexString(string.Concat(this.PayloadParts)),
            _ => throw new FormatException("Format must be text or hex.")
        };

        var result = await context.Radio.TransmitAsync(payload, cancellationToken);
        await context.Output.WriteLineAsync($"TxDone: {payload.Length} bytes, time-on-air {result.TimeOnAir.TotalMilliseconds:F1} ms.");
        return CommandOutcome.Continue;
    }
}