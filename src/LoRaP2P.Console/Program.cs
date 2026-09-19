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
        return await parseResult.MapResult(options => RunAsync(options, shutdown.Token), HandleParseErrors);
    }

    private static async Task<int> RunAsync(StartupOptions options, CancellationToken cancellationToken)
    {
        try
        {
            var configurationPath = options.ResolveConfigurationPath();
            var configuration = await LoadConfigurationAsync(configurationPath, cancellationToken);
            configuration.Validate();

            if (options.Check)
            {
                return await RunHardwareCheckAsync(configuration, cancellationToken);
            }

            using var transport = new SystemDeviceRfm9xTransport(configuration);
            await using var radio = new Rfm9xRadio(transport);
            await radio.ResetAsync(cancellationToken);
            var version = await radio.ProbeAsync(cancellationToken);
            await radio.ConfigureAsync(configuration, cancellationToken);

            System.Console.WriteLine($"RFM95 detected (version 0x{version:X2}). Raw LoRa EU868 console ready.");
            System.Console.WriteLine("Enter 'help' for commands. Ctrl+C exits safely.");
            return await InteractiveConsole.RunAsync(
                radio,
                configuration,
                System.Console.In,
                System.Console.Out,
                System.Console.Error,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!System.Console.IsOutputRedirected)
            {
                // Clear the current console line before exiting (suppress "^C")
                System.Console.Write("\r\u001b[2K");
            }

            return 130;
        }
        catch (Exception exception) when (IsExpectedStartupException(exception))
        {
            await System.Console.Error.WriteLineAsync($"Startup failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunHardwareCheckAsync(
        RadioConfiguration configuration,
        CancellationToken cancellationToken)
    {
        using var bonnet = new SystemRadioBonnetCheckUi();
        try
        {
            using var transport = new SystemDeviceRfm9xTransport(
                configuration,
                enableDio0Events: false);
            await using var radio = new Rfm9xRadio(transport);
            await RadioHardwareCheck.RunAsync(
                radio,
                configuration,
                bonnet,
                System.Console.Out,
                cancellationToken);
            return 0;
        }
        catch (Exception exception) when (IsExpectedStartupException(exception))
        {
            bonnet.ShowRadioStatus(detected: false);
            throw;
        }
    }

    private static Task<int> HandleParseErrors(IEnumerable<Error> errors)
    {
        var informationRequested = errors.All(error => error is HelpRequestedError or VersionRequestedError);
        return Task.FromResult(informationRequested ? 0 : 1);
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