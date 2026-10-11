using CommandLine;

namespace LoRaP2P.SpeechDisplay;

internal sealed class SpeechDisplayOptions
{
    [Option("model", HelpText = "Local Vosk model; downloads to the user directory when omitted.")]
    public string ModelPath { get; set; } = "";

    [Option("model-url", Default = VoskModelStore.DefaultModelUrl, HelpText = "HTTPS URL of the Vosk model ZIP to download automatically.")]
    public string ModelUrl { get; set; } = VoskModelStore.DefaultModelUrl;

    [Option("vosk-library", HelpText = "Optional native Vosk library path; defaults to libvosk.so beside the CLI.")]
    public string VoskLibraryPath { get; set; } = "";

    public string ResolvedVoskLibraryPath => string.IsNullOrWhiteSpace(this.VoskLibraryPath)
        ? Path.Combine(AppContext.BaseDirectory, "libvosk.so")
        : Path.GetFullPath(this.VoskLibraryPath);

    [Option("device", Default = "default", HelpText = "ALSA capture device, for example plughw:1,0.")]
    public string Device { get; set; } = "default";

    [Option("scroll-ms", Default = 1000, HelpText = "Time per new text line in milliseconds (100 to 10000).")]
    public int ScrollMilliseconds { get; set; } = 1000;

    [Option("no-display", HelpText = "Terminal output only; no GPIO or I2C access.")]
    public bool NoDisplay { get; set; }

    public void Validate()
    {
        if (!string.IsNullOrWhiteSpace(this.ModelPath) && !VoskModelStore.IsModelDirectory(this.ModelPath))
        {
            throw new ArgumentException("--model must point to a fully extracted Vosk speech model.");
        }

        if (string.IsNullOrWhiteSpace(this.ModelPath))
        {
            VoskModelStore.ParseModelUrl(this.ModelUrl);
        }

        if (!File.Exists(this.ResolvedVoskLibraryPath))
        {
            throw new ArgumentException($"Vosk library not found: {this.ResolvedVoskLibraryPath}. "
                + "Use a linux-arm64 publish or specify --vosk-library.");
        }

        if (string.IsNullOrWhiteSpace(this.Device))
        {
            throw new ArgumentException("--device must not be empty.");
        }

        if (this.ScrollMilliseconds is < 100 or > 10000)
        {
            throw new ArgumentException("--scroll-ms must be between 100 and 10000.");
        }
    }
}