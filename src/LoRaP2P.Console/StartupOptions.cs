using CommandLine;

namespace LoRaP2P.ConsoleApp;

internal sealed class StartupOptions
{
    [Option('c', "config", Required = false, HelpText = "Path to the radio configuration file.")]
    public string? ConfigurationPath { get; set; }

    public string ResolveConfigurationPath() => ConfigurationPath is null
        ? Path.Combine(AppContext.BaseDirectory, "appsettings.json")
        : Path.GetFullPath(ConfigurationPath);
}
