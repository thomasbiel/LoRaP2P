using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace LoRaP2P.SpeechDisplay;

internal sealed class VoskModelStore(HttpClient httpClient, string modelsDirectory)
{
    public const string DefaultModelUrl = "https://alphacephei.com/vosk/models/vosk-model-small-de-0.15.zip";

    public static string UserModelsDirectory
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
            {
                throw new IOException("Benutzerordner konnte nicht ermittelt werden.");
            }

            return Path.Combine(home, "LoRaP2P", "models");
        }
    }

    public static Uri ParseModelUrl(string modelUrl)
    {
        if (!Uri.TryCreate(modelUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("--model-url muss eine absolute HTTPS-URL ohne Zugangsdaten oder Fragment sein.");
        }

        return uri;
    }

    public static bool IsModelDirectory(string path) =>
        File.Exists(Path.Combine(path, "am", "final.mdl"))
        && File.Exists(Path.Combine(path, "conf", "mfcc.conf"))
        && File.Exists(Path.Combine(path, "conf", "model.conf"))
        && (File.Exists(Path.Combine(path, "graph", "HCLG.fst"))
            || (File.Exists(Path.Combine(path, "graph", "HCLr.fst"))
                && File.Exists(Path.Combine(path, "graph", "Gr.fst"))));

    public async Task<string> GetModelAsync(
        string modelPath,
        string modelUrl,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(modelPath))
        {
            if (!IsModelDirectory(modelPath))
            {
                throw new ArgumentException("--model muss auf ein vollstaendig entpacktes Vosk-Sprachmodell zeigen.");
            }

            return Path.GetFullPath(modelPath);
        }

        var uri = ParseModelUrl(modelUrl);
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var cachedModel = Path.Combine(modelsDirectory, key);
        if (IsModelDirectory(cachedModel))
        {
            await output.WriteLineAsync($"Sprachmodell aus Cache: {cachedModel}").ConfigureAwait(false);
            return cachedModel;
        }

        if (Directory.Exists(cachedModel))
        {
            throw new IOException($"Modellcache unvollstaendig: {cachedModel}. Ordner entfernen und erneut starten.");
        }

        Directory.CreateDirectory(modelsDirectory);
        var staging = Path.Combine(modelsDirectory, $".download-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            await output.WriteLineAsync($"Sprachmodell herunterladen: {uri}").ConfigureAwait(false);
            var archivePath = Path.Combine(staging, "model.zip");
            using (var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.RequestMessage?.RequestUri is { Scheme: not "https" })
                {
                    throw new IOException("Sprachmodell-Download wurde auf eine unsichere URL umgeleitet.");
                }

                await using var archiveFile = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true);
                await response.Content.CopyToAsync(archiveFile, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await output.WriteLineAsync("Sprachmodell entpacken...").ConfigureAwait(false);
            var extracted = Path.Combine(staging, "extracted");
            ZipFile.ExtractToDirectory(archivePath, extracted);
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = new[] { extracted }.Concat(Directory.GetDirectories(extracted))
                .Where(IsModelDirectory).ToArray();
            if (candidates.Length != 1)
            {
                throw new InvalidDataException("ZIP muss genau ein vollstaendiges Vosk-Sprachmodell enthalten.");
            }

            try
            {
                Directory.Move(candidates[0], cachedModel);
            }
            catch (IOException) when (IsModelDirectory(cachedModel))
            {
            }

            await output.WriteLineAsync($"Sprachmodell bereit: {cachedModel}").ConfigureAwait(false);
            return cachedModel;
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }
}