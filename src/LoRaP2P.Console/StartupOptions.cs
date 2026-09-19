using CommandLine;

namespace LoRaP2P.Console;

internal sealed class StartupOptions
{
    [Option('c', "config", Required = false, HelpText = "Path to the radio configuration file.")]
    public string? ConfigurationPath { get; set; }

    [Option("check", Required = false, HelpText = "Check the RFM9x, OLED, and buttons until cancelled.")]
    public bool Check { get; set; }

    public string ResolveConfigurationPath() => this.ConfigurationPath is null
        ? Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        : Path.GetFullPath(this.ConfigurationPath);
}
