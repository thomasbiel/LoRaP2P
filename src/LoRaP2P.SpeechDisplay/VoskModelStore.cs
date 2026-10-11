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
                throw new IOException("Could not determine the user directory.");
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
            throw new ArgumentException("--model-url must be an absolute HTTPS URL without credentials or a fragment.");
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
                throw new ArgumentException("--model must point to a fully extracted Vosk speech model.");
            }

            return Path.GetFullPath(modelPath);
        }

        var uri = ParseModelUrl(modelUrl);
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var cachedModel = Path.Combine(modelsDirectory, key);
        if (IsModelDirectory(cachedModel))
        {
            await output.WriteLineAsync($"Using cached speech model: {cachedModel}").ConfigureAwait(false);
            return cachedModel;
        }

        if (Directory.Exists(cachedModel))
        {
            throw new IOException($"Incomplete model cache: {cachedModel}. Remove the directory and restart.");
        }

        Directory.CreateDirectory(modelsDirectory);
        var staging = Path.Combine(modelsDirectory, $".download-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            await output.WriteLineAsync($"Downloading speech model: {uri}").ConfigureAwait(false);
            var archivePath = Path.Combine(staging, "model.zip");
            using (var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                if (response.RequestMessage?.RequestUri is { Scheme: not "https" })
                {
                    throw new IOException("The speech model download was redirected to an insecure URL.");
                }

                await using var archiveFile = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true);
                await response.Content.CopyToAsync(archiveFile, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            await output.WriteLineAsync("Extracting speech model...").ConfigureAwait(false);
            var extracted = Path.Combine(staging, "extracted");
            ZipFile.ExtractToDirectory(archivePath, extracted);
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = new[] { extracted }.Concat(Directory.GetDirectories(extracted))
                .Where(IsModelDirectory).ToArray();
            if (candidates.Length != 1)
            {
                throw new InvalidDataException("The ZIP must contain exactly one complete Vosk speech model.");
            }

            try
            {
                Directory.Move(candidates[0], cachedModel);
            }
            catch (IOException) when (IsModelDirectory(cachedModel))
            {
            }

            await output.WriteLineAsync($"Speech model ready: {cachedModel}").ConfigureAwait(false);
            return cachedModel;
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }
    }
}