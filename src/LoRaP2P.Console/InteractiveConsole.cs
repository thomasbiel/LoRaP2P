using LoRaP2P.Console.Commands;
using LoRaP2P.Protocol;
using LoRaP2P.Protocol.Frames;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console;

internal static class InteractiveConsole
{
    public static async Task<int> RunAsync(
        ILoraRadio radio,
        RadioConfiguration initialConfiguration,
        P2pConsoleSession p2p,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, initialConfiguration, p2p, output);

        while (!cancellationToken.IsCancellationRequested)
        {
            await output.WriteAsync("lora> ");
            await output.FlushAsync(cancellationToken);
            var line = await input.ReadLineAsync(cancellationToken);
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
                var outcome = await parser.ParseAndExecuteAsync(line, context, cancellationToken);
                if (outcome == CommandOutcome.Exit)
                {
                    return 0;
                }
            }
            catch (Exception exception) when (IsExpectedCommandException(exception))
            {
                await error.WriteLineAsync($"Command failed: {exception.Message}");
            }
        }

        return 0;
    }

    private static bool IsExpectedCommandException(Exception exception) =>
        exception is ArgumentException
            or FormatException
            or InvalidDataException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or P2pDeliveryException
            or P2pProtocolException
            or Rfm9xException
            or TimeoutException;
}
