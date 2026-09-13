using System.Globalization;
using CommandLine;

namespace LoRaP2P.ConsoleApp.Commands;

[Verb("register", HelpText = "Read one or more radio registers.")]
internal sealed class RegisterVerb : IConsoleVerb
{
    [Value(0, MetaName = "operation", Required = true, HelpText = "read or dump")]
    public string Operation { get; set; } = string.Empty;

    [Value(1, MetaName = "address", Required = false, HelpText = "Hexadecimal start address")]
    public string? Address { get; set; }

    [Value(2, MetaName = "count", Required = false, HelpText = "Number of registers")]
    public int? Count { get; set; }

    public async Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        (byte startAddress, int count) = Operation.ToLowerInvariant() switch
        {
            "dump" when Address is null && Count is null => ((byte)0x01, 0x42),
            "read" when Address is not null => (ParseByte(Address), Count ?? 1),
            _ => throw new FormatException(
                "Usage: register read <address> [count] or register dump"),
        };

        IReadOnlyDictionary<byte, byte> registers = await context.Radio.ReadRegistersAsync(
            startAddress,
            count,
            cancellationToken);
        foreach ((byte address, byte value) in registers)
        {
            context.Output.WriteLine(
                $"0x{address:X2}: 0x{value:X2}  {Convert.ToString(value, 2).PadLeft(8, '0')}");
        }

        return CommandOutcome.Continue;
    }

    private static byte ParseByte(string value)
    {
        string normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte result)
            ? result
            : throw new FormatException($"Invalid register address: '{value}'.");
    }
}