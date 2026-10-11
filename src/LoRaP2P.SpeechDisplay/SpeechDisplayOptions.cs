using CommandLine;

namespace LoRaP2P.SpeechDisplay;

internal sealed class SpeechDisplayOptions
{
    [Option("model", HelpText = "Lokales Vosk-Modell; ohne Angabe automatischer Download in den Benutzerordner.")]
    public string ModelPath { get; set; } = "";

    [Option("model-url", Default = VoskModelStore.DefaultModelUrl, HelpText = "HTTPS-URL des automatisch geladenen Vosk-Modell-ZIPs.")]
    public string ModelUrl { get; set; } = VoskModelStore.DefaultModelUrl;

    [Option("vosk-library", HelpText = "Optionaler Pfad zur nativen Vosk-Bibliothek; Standard: libvosk.so neben dem CLI.")]
    public string VoskLibraryPath { get; set; } = "";

    public string ResolvedVoskLibraryPath => string.IsNullOrWhiteSpace(this.VoskLibraryPath)
        ? Path.Combine(AppContext.BaseDirectory, "libvosk.so")
        : Path.GetFullPath(this.VoskLibraryPath);

    [Option("device", Default = "default", HelpText = "ALSA-Aufnahmegeraet, z. B. plughw:1,0.")]
    public string Device { get; set; } = "default";

    [Option("scroll-ms", Default = 1000, HelpText = "Zeit pro neuer Textzeile in Millisekunden (100 bis 10000).")]
    public int ScrollMilliseconds { get; set; } = 1000;

    [Option("no-display", HelpText = "Nur Terminalausgabe; kein GPIO-/I2C-Zugriff.")]
    public bool NoDisplay { get; set; }

    public void Validate()
    {
        if (!string.IsNullOrWhiteSpace(this.ModelPath) && !VoskModelStore.IsModelDirectory(this.ModelPath))
        {
            throw new ArgumentException("--model muss auf ein vollstaendig entpacktes Vosk-Sprachmodell zeigen.");
        }

        if (string.IsNullOrWhiteSpace(this.ModelPath))
        {
            VoskModelStore.ParseModelUrl(this.ModelUrl);
        }

        if (!File.Exists(this.ResolvedVoskLibraryPath))
        {
            throw new ArgumentException($"Vosk-Bibliothek fehlt: {this.ResolvedVoskLibraryPath}. "
                + "linux-arm64-Publish verwenden oder --vosk-library angeben.");
        }

        if (string.IsNullOrWhiteSpace(this.Device))
        {
            throw new ArgumentException("--device darf nicht leer sein.");
        }

        if (this.ScrollMilliseconds is < 100 or > 10000)
        {
            throw new ArgumentException("--scroll-ms muss zwischen 100 und 10000 liegen.");
        }
    }
}