using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Commands;

internal sealed class CommandContext(
    ILoraRadio radio,
    RadioConfiguration configuration,
    P2pConsoleSession p2p,
    TextWriter output)
{
    public ILoraRadio Radio { get; } = radio;

    public RadioConfiguration Configuration { get; set; } = configuration;

    public P2pConsoleSession P2p { get; } = p2p;

    public TextWriter Output { get; } = output;
}
