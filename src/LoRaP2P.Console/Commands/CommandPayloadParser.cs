using System.Text;

namespace LoRaP2P.Console.Commands;

internal static class CommandPayloadParser
{
    public static byte[] Parse(string format, IEnumerable<string> payloadParts) =>
        format.ToLowerInvariant() switch
        {
            "text" => Encoding.UTF8.GetBytes(string.Join(' ', payloadParts)),
            "hex" => Convert.FromHexString(string.Concat(payloadParts)),
            _ => throw new FormatException("Format must be text or hex.")
        };

    public static byte[] ParseText(string format, IEnumerable<string> payloadParts)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(format, "text"))
        {
            throw new FormatException("Broadcast format must be text.");
        }

        return Encoding.UTF8.GetBytes(string.Join(' ', payloadParts));
    }
}
