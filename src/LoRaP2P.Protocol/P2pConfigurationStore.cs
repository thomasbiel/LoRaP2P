using System.Text.Json;
using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Protocol;

public sealed class P2pConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string path;

    public P2pConfigurationStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = Path.GetFullPath(path);
    }

    public async Task<P2pConfiguration?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(this.path))
        {
            return null;
        }

        await using var stream = new FileStream(
            this.path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        ConfigurationDto? dto;
        try
        {
            dto = await JsonSerializer.DeserializeAsync<ConfigurationDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"P2P configuration '{this.path}' contains invalid JSON.", exception);
        }

        if (dto is null)
        {
            throw new InvalidDataException($"P2P configuration '{this.path}' is empty.");
        }

        try
        {
            var configuration = dto.ToConfiguration();
            configuration.Validate();
            return configuration;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"P2P configuration '{this.path}' is invalid: {exception.Message}", exception);
        }
    }

    public async Task SaveAsync(P2pConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        var directory = Path.GetDirectoryName(this.path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(this.path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        ConfigurationDto.FromConfiguration(configuration),
                        JsonOptions,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, this.path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record ConfigurationDto
    {
        public ushort LocalNodeId { get; init; }

        public ushort[] Peers { get; init; } = [];

        public TimeSpan AckTimeout { get; init; } = TimeSpan.FromSeconds(2);

        public int MaxRetries { get; init; } = 3;

        public TimeSpan BackoffMinimum { get; init; } = TimeSpan.FromMilliseconds(250);

        public TimeSpan BackoffMaximum { get; init; } = TimeSpan.FromSeconds(1);

        public TimeSpan AckTurnaroundDelay { get; init; } = TimeSpan.FromMilliseconds(100);

        public TimeSpan DuplicateWindow { get; init; } = TimeSpan.FromMinutes(10);

        public int DuplicateEntriesPerPeer { get; init; } = 64;

        public P2pConfiguration ToConfiguration() =>
            new()
            {
                LocalNodeId = new P2pNodeId(this.LocalNodeId),
                Peers = [.. (this.Peers ?? throw new ArgumentException("Peers cannot be null.")).Select(static value => new P2pNodeId(value))],
                Options = new P2pOptions
                {
                    AckTimeout = this.AckTimeout,
                    MaxRetries = this.MaxRetries,
                    BackoffMinimum = this.BackoffMinimum,
                    BackoffMaximum = this.BackoffMaximum,
                    AckTurnaroundDelay = this.AckTurnaroundDelay,
                    DuplicateWindow = this.DuplicateWindow,
                    DuplicateEntriesPerPeer = this.DuplicateEntriesPerPeer
                }
            };

        public static ConfigurationDto FromConfiguration(P2pConfiguration configuration) =>
            new()
            {
                LocalNodeId = configuration.LocalNodeId.Value,
                Peers = [.. configuration.Peers.Select(static peer => peer.Value)],
                AckTimeout = configuration.Options.AckTimeout,
                MaxRetries = configuration.Options.MaxRetries,
                BackoffMinimum = configuration.Options.BackoffMinimum,
                BackoffMaximum = configuration.Options.BackoffMaximum,
                AckTurnaroundDelay = configuration.Options.AckTurnaroundDelay,
                DuplicateWindow = configuration.Options.DuplicateWindow,
                DuplicateEntriesPerPeer = configuration.Options.DuplicateEntriesPerPeer
            };
    }
}
