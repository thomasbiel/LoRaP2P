using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;
using LoRaP2P.Radio.Rfm9x;
using LoRaP2P.Radio.Rfm9x.Transport;

namespace LoRaP2P.Console;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<int> Main(string[] args)
    {
        using var shutdown = new CancellationTokenSource();
        System.Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        using Parser parser = new(settings =>
        {
            settings.CaseSensitive = false;
            settings.HelpWriter = System.Console.Error;
        });
        
        var parseResult = parser.ParseArguments<StartupOptions>(args);
        if (parseResult is NotParsed<StartupOptions> notParsed)
        {
            var informationRequested = notParsed.Errors.All(error => error is HelpRequestedError or VersionRequestedError);
            return informationRequested ? 0 : 1;
        }

        var options = ((Parsed<StartupOptions>)parseResult).Value;

        try
        {
            var configurationPath = options.ResolveConfigurationPath();
            var configuration = await LoadConfigurationAsync(configurationPath, shutdown.Token);
            configuration.Validate();

            using var transport = new SystemDeviceRfm9xTransport(configuration);
            await using var radio = new Rfm9xRadio(transport);
            await radio.ResetAsync(shutdown.Token);
            var version = await radio.ProbeAsync(shutdown.Token);
            await radio.ConfigureAsync(configuration, shutdown.Token);

            System.Console.WriteLine($"RFM95 detected (version 0x{version:X2}). Raw LoRa EU868 console ready.");
            System.Console.WriteLine("Enter 'help' for commands. Ctrl+C exits safely.");
            return await InteractiveConsole.RunAsync(
                radio,
                configuration,
                System.Console.In,
                System.Console.Out,
                System.Console.Error,
                shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            System.Console.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (Exception exception) when (IsExpectedStartupException(exception))
        {
            await System.Console.Error.WriteLineAsync($"Startup failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<RadioConfiguration> LoadConfigurationAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var configuration = await JsonSerializer.DeserializeAsync<RadioConfiguration>(
            stream,
            JsonOptions,
            cancellationToken);
        
        return configuration ?? throw new JsonException("Configuration file contains no radio configuration.");
    }

    private static bool IsExpectedStartupException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or PlatformNotSupportedException
            or Rfm9xException;
}