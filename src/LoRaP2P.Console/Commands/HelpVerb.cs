using CommandLine;

namespace LoRaP2P.ConsoleApp.Commands;

[Verb("help", HelpText = "Show available commands.")]
internal sealed class HelpVerb : IConsoleVerb
{
    public Task<CommandOutcome> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken)
    {
        context.Output.WriteLine("probe                              Read and verify chip version");
        context.Output.WriteLine("status                             Show mode and radio profile");
        context.Output.WriteLine("reset                              Reset and reconfigure radio");
        context.Output.WriteLine("configure frequency <MHz>          Set EU868 frequency");
        context.Output.WriteLine("configure sf <7-12>                Set spreading factor");
        context.Output.WriteLine("configure bandwidth <125|250|500>  Set bandwidth in kHz");
        context.Output.WriteLine("configure power <2-14>             Set output power in dBm");
        context.Output.WriteLine("send text <value>                  Transmit UTF-8 payload");
        context.Output.WriteLine("send hex <hex-bytes>               Transmit hexadecimal payload");
        context.Output.WriteLine("receive [timeout-seconds]          Wait for one packet");
        context.Output.WriteLine("register read <address> [count]    Read register values");
        context.Output.WriteLine("register dump                      Read registers 0x01 through 0x42");
        context.Output.WriteLine("quit                               Exit");
        return Task.FromResult(CommandOutcome.Continue);
    }
}