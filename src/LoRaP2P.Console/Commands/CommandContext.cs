using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console.Commands;

internal sealed class CommandContext(
    ILoraRadio radio,
    RadioConfiguration configuration,
    TextWriter output)
{
    public ILoraRadio Radio { get; } = radio;

    public RadioConfiguration Configuration { get; set; } = configuration;

    public TextWriter Output { get; } = output;
}
