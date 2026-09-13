using System.Globalization;
using CommandLine;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Commands;

[Verb("configure", HelpText = "Change frequency, spreading factor, bandwidth, or output power.")]
internal sealed class ConfigureVerb : IConsoleVerb
{
    [Value(0, MetaName = "setting", Required = true, HelpText = "frequency, sf, bandwidth, or power")]
    public string Setting { get; set; } = string.Empty;

    [Value(1, MetaName = "value", Required = true, HelpText = "New setting value")]
    public string Value { get; set; } = string.Empty;

    public async Task<CommandOutcome> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var updated = this.Setting.ToLowerInvariant() switch
        {
            "frequency" => context.Configuration with { FrequencyHertz = ParseDouble(this.Value, "frequency") * 1_000_000 },
            "sf" => context.Configuration with { SpreadingFactor = (SpreadingFactor)ParseInteger(this.Value, "spreading factor") },
            "bandwidth" => context.Configuration with { Bandwidth = (SignalBandwidth)(ParseInteger(this.Value, "bandwidth") * 1_000) },
            "power" => context.Configuration with { OutputPowerDbm = ParseInteger(this.Value, "power") },
            _ => throw new FormatException("Setting must be frequency, sf, bandwidth, or power.")
        };

        updated.Validate();
        await context.Radio.ConfigureAsync(updated, cancellationToken);
        context.Configuration = updated;
        await context.Output.WriteLineAsync("Radio configuration applied.");
        return CommandOutcome.Continue;
    }

    private static int ParseInteger(string value, string name) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"Invalid {name}: '{value}'.");

    private static double ParseDouble(string value, string name) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new FormatException($"Invalid {name}: '{value}'. Use '.' as decimal separator.");
}