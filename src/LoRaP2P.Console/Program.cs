using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoRaP2P.Radio.Rfm9x;
using LoRaP2P.Radio.Rfm9x.Transport;

namespace LoRaP2P.ConsoleApp;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<int> Main(string[] args)
    {
        using var shutdown = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            string configurationPath = GetConfigurationPath(args);
            RadioConfiguration configuration = await LoadConfigurationAsync(configurationPath, shutdown.Token);
            configuration.Validate();

            using var transport = new SystemDeviceRfm9xTransport(configuration);
            await using var radio = new Rfm9xRadio(transport);
            await radio.ResetAsync(shutdown.Token);
            byte version = await radio.ProbeAsync(shutdown.Token);
            await radio.ConfigureAsync(configuration, shutdown.Token);

            Console.WriteLine($"RFM95 detected (version 0x{version:X2}). Raw LoRa EU868 console ready.");
            Console.WriteLine("Enter 'help' for commands. Ctrl+C exits safely.");
            return await RunConsoleAsync(radio, configuration, shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (Exception exception) when (IsExpectedStartupException(exception))
        {
            Console.Error.WriteLine($"Startup failed: {exception.Message}");
            return 1;
        }
    }

    private static async Task<int> RunConsoleAsync(
        Rfm9xRadio radio,
        RadioConfiguration initialConfiguration,
        CancellationToken cancellationToken)
    {
        RadioConfiguration configuration = initialConfiguration;

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("lora> ");
            string? line = await Console.In.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return 0;
            }

            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            try
            {
                string command = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
                switch (command)
                {
                    case "help":
                        PrintHelp();
                        break;
                    case "probe":
                        Console.WriteLine($"SX1276 version: 0x{await radio.ProbeAsync(cancellationToken):X2}");
                        break;
                    case "status":
                        await PrintStatusAsync(radio, cancellationToken);
                        break;
                    case "reset":
                        await radio.ResetAsync(cancellationToken);
                        await radio.ProbeAsync(cancellationToken);
                        await radio.ConfigureAsync(configuration, cancellationToken);
                        Console.WriteLine("Radio reset and configured.");
                        break;
                    case "configure":
                        configuration = await ConfigureAsync(radio, configuration, line, cancellationToken);
                        break;
                    case "send":
                        await SendAsync(radio, line, cancellationToken);
                        break;
                    case "receive":
                        await ReceiveAsync(radio, line, cancellationToken);
                        break;
                    case "register":
                        await ReadRegistersAsync(radio, line, cancellationToken);
                        break;
                    case "quit":
                    case "exit":
                        return 0;
                    default:
                        Console.WriteLine($"Unknown command '{command}'. Enter 'help'.");
                        break;
                }
            }
            catch (Exception exception) when (IsExpectedCommandException(exception))
            {
                Console.Error.WriteLine($"Command failed: {exception.Message}");
            }
        }

        return 0;
    }

    private static async Task<RadioConfiguration> ConfigureAsync(
        Rfm9xRadio radio,
        RadioConfiguration current,
        string line,
        CancellationToken cancellationToken)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            throw new FormatException("Usage: configure <frequency|sf|bandwidth|power> <value>");
        }

        RadioConfiguration updated = parts[1].ToLowerInvariant() switch
        {
            "frequency" => current with
            {
                FrequencyHertz = ParseDouble(parts[2], "frequency") * 1_000_000,
            },
            "sf" => current with
            {
                SpreadingFactor = (SpreadingFactor)ParseInteger(parts[2], "spreading factor"),
            },
            "bandwidth" => current with
            {
                Bandwidth = (SignalBandwidth)(ParseInteger(parts[2], "bandwidth") * 1_000),
            },
            "power" => current with
            {
                OutputPowerDbm = ParseInteger(parts[2], "power"),
            },
            _ => throw new FormatException("Setting must be frequency, sf, bandwidth, or power."),
        };

        updated.Validate();
        await radio.ConfigureAsync(updated, cancellationToken);
        Console.WriteLine("Radio configuration applied.");
        return updated;
    }

    private static async Task SendAsync(Rfm9xRadio radio, string line, CancellationToken cancellationToken)
    {
        byte[] payload;
        if (line.StartsWith("send text ", StringComparison.OrdinalIgnoreCase))
        {
            payload = Encoding.UTF8.GetBytes(line[10..]);
        }
        else if (line.StartsWith("send hex ", StringComparison.OrdinalIgnoreCase))
        {
            payload = Convert.FromHexString(line[9..].Replace(" ", string.Empty, StringComparison.Ordinal));
        }
        else
        {
            throw new FormatException("Usage: send text <value> or send hex <hex-bytes>");
        }

        TransmitResult result = await radio.TransmitAsync(payload, cancellationToken);
        Console.WriteLine($"TxDone: {payload.Length} bytes, time-on-air {result.TimeOnAir.TotalMilliseconds:F1} ms.");
    }

    private static async Task ReceiveAsync(Rfm9xRadio radio, string line, CancellationToken cancellationToken)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 2)
        {
            throw new FormatException("Usage: receive [timeout-seconds]");
        }

        double timeoutSeconds = parts.Length == 2 ? ParseDouble(parts[1], "timeout") : 10;
        ReceivedPacket? packet = await radio.ReceiveSingleAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
        if (packet is null)
        {
            Console.WriteLine("Receive timeout.");
            return;
        }

        Console.WriteLine(
            $"RxDone: {Convert.ToHexString(packet.Payload)} | CRC {(packet.IsCrcValid ? "valid" : "invalid")} | "
            + $"RSSI {packet.PacketRssiDbm} dBm | SNR {packet.PacketSnrDb:F2} dB");
    }

    private static async Task ReadRegistersAsync(Rfm9xRadio radio, string line, CancellationToken cancellationToken)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        byte startAddress;
        int count;

        if (parts.Length == 2 && StringComparer.OrdinalIgnoreCase.Equals(parts[1], "dump"))
        {
            startAddress = 0x01;
            count = 0x42;
        }
        else if (parts.Length is 3 or 4 && StringComparer.OrdinalIgnoreCase.Equals(parts[1], "read"))
        {
            startAddress = ParseByte(parts[2]);
            count = parts.Length == 4 ? ParseInteger(parts[3], "count") : 1;
        }
        else
        {
            throw new FormatException("Usage: register read <address> [count] or register dump");
        }

        IReadOnlyDictionary<byte, byte> registers = await radio.ReadRegistersAsync(startAddress, count, cancellationToken);
        foreach ((byte address, byte value) in registers)
        {
            Console.WriteLine($"0x{address:X2}: 0x{value:X2}  {Convert.ToString(value, 2).PadLeft(8, '0')}");
        }
    }

    private static async Task PrintStatusAsync(Rfm9xRadio radio, CancellationToken cancellationToken)
    {
        RadioStatus status = await radio.GetStatusAsync(cancellationToken);
        RadioConfiguration configuration = status.Configuration;
        Console.WriteLine($"Chip: 0x{status.ChipVersion:X2}; mode: {status.Mode}; IRQ: 0x{status.IrqFlags:X2}");
        Console.WriteLine(
            $"Frequency: {configuration.FrequencyHertz / 1_000_000:F3} MHz; SF: {(int)configuration.SpreadingFactor}; "
            + $"BW: {(int)configuration.Bandwidth / 1_000} kHz; power: {configuration.OutputPowerDbm} dBm");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("probe                              Read and verify chip version");
        Console.WriteLine("status                             Show mode and radio profile");
        Console.WriteLine("reset                              Reset and reconfigure radio");
        Console.WriteLine("configure frequency <MHz>          Set EU868 frequency");
        Console.WriteLine("configure sf <7-12>                Set spreading factor");
        Console.WriteLine("configure bandwidth <125|250|500>  Set bandwidth in kHz");
        Console.WriteLine("configure power <2-14>             Set output power in dBm");
        Console.WriteLine("send text <value>                  Transmit UTF-8 payload");
        Console.WriteLine("send hex <hex-bytes>               Transmit hexadecimal payload");
        Console.WriteLine("receive [timeout-seconds]           Wait for one packet");
        Console.WriteLine("register read <address> [count]    Read register values");
        Console.WriteLine("register dump                      Read registers 0x01 through 0x42");
        Console.WriteLine("quit                               Exit");
    }

    private static string GetConfigurationPath(string[] args)
    {
        if (args.Length == 0)
        {
            return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        }

        if (args.Length == 2 && StringComparer.OrdinalIgnoreCase.Equals(args[0], "--config"))
        {
            return Path.GetFullPath(args[1]);
        }

        throw new ArgumentException("Usage: LoRaP2P.Console [--config <path>]");
    }

    private static async Task<RadioConfiguration> LoadConfigurationAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        RadioConfiguration? configuration = await JsonSerializer.DeserializeAsync<RadioConfiguration>(
            stream,
            JsonOptions,
            cancellationToken);
        return configuration ?? throw new JsonException("Configuration file contains no radio configuration.");
    }

    private static int ParseInteger(string value, string name)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : throw new FormatException($"Invalid {name}: '{value}'.");
    }

    private static double ParseDouble(string value, string name)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : throw new FormatException($"Invalid {name}: '{value}'. Use '.' as decimal separator.");
    }

    private static byte ParseByte(string value)
    {
        string normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return byte.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte result)
            ? result
            : throw new FormatException($"Invalid register address: '{value}'.");
    }

    private static bool IsExpectedStartupException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or PlatformNotSupportedException
            or Rfm9xException;

    private static bool IsExpectedCommandException(Exception exception) =>
        exception is ArgumentException
            or FormatException
            or Rfm9xException
            or TimeoutException;
}