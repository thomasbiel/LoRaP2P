using LoRaP2P.ConsoleApp.Commands;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.ConsoleApp;

internal static class InteractiveConsole
{
    public static async Task<int> RunAsync(
        ILoraRadio radio,
        RadioConfiguration initialConfiguration,
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        using ConsoleCommandParser parser = new(error);
        CommandContext context = new(radio, initialConfiguration, output);

        while (!cancellationToken.IsCancellationRequested)
        {
            output.Write("lora> ");
            await output.FlushAsync(cancellationToken);
            string? line = await input.ReadLineAsync(cancellationToken);
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
                CommandOutcome outcome = await parser.ParseAndExecuteAsync(line, context, cancellationToken);
                if (outcome == CommandOutcome.Exit)
                {
                    return 0;
                }
            }
            catch (Exception exception) when (IsExpectedCommandException(exception))
            {
                error.WriteLine($"Command failed: {exception.Message}");
            }
        }

        return 0;
    }

    private static bool IsExpectedCommandException(Exception exception) =>
        exception is ArgumentException
            or FormatException
            or Rfm9xException
            or TimeoutException;
}
