using System.Globalization;
using System.Text;
using System.Text.Json;
using LoRaP2P.Protocol;
using LoRaP2P.Protocol.Frames;

namespace LoRaP2P.Console;

internal sealed class P2pSessionLog : IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Lock syncLock = new();
    private readonly string directory;
    private readonly TimeProvider timeProvider;
    private readonly Dictionary<SessionKey, StreamWriter> writers = [];
    private bool disposed;

    public P2pSessionLog(string? directory = null, TimeProvider? timeProvider = null)
    {
        this.directory = Path.GetFullPath(directory ?? GetDefaultDirectory());
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public string DirectoryPath => this.directory;

    public void AppendReceived(P2pNodeId localNodeId, P2pReceivedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        this.Append(
            new P2pSessionMessage
            {
                Timestamp = message.ReceivedAt,
                Direction = "incoming",
                LocalNodeId = localNodeId.Value,
                PeerNodeId = message.Sender.Value,
                SequenceNumber = message.SequenceNumber,
                PayloadType = TryDecodeText(message.Payload, out var text) ? "text" : "hex",
                Text = text,
                Hex = Convert.ToHexString(message.Payload),
                Status = "received",
                RssiDbm = message.RssiDbm,
                SnrDb = message.SnrDb
            });
    }

    public void AppendSent(
        P2pNodeId localNodeId,
        P2pNodeId peer,
        uint sequenceNumber,
        ReadOnlySpan<byte> payload,
        string payloadType,
        string status,
        int attempts)
    {
        var normalizedPayloadType = NormalizePayloadType(payloadType);
        this.Append(
            new P2pSessionMessage
            {
                Timestamp = this.timeProvider.GetUtcNow(),
                Direction = "outgoing",
                LocalNodeId = localNodeId.Value,
                PeerNodeId = peer.Value,
                SequenceNumber = sequenceNumber,
                PayloadType = normalizedPayloadType,
                Text = normalizedPayloadType == "text" ? Encoding.UTF8.GetString(payload) : null,
                Hex = Convert.ToHexString(payload),
                Status = status,
                Attempts = attempts
            });
    }

    public void AppendBroadcast(
        P2pNodeId localNodeId,
        uint sequenceNumber,
        ReadOnlySpan<byte> payload,
        string payloadType)
    {
        var normalizedPayloadType = NormalizePayloadType(payloadType);
        this.Append(
            new P2pSessionMessage
            {
                Timestamp = this.timeProvider.GetUtcNow(),
                Direction = "broadcast",
                LocalNodeId = localNodeId.Value,
                PeerNodeId = ushort.MaxValue,
                SequenceNumber = sequenceNumber,
                PayloadType = normalizedPayloadType,
                Text = normalizedPayloadType == "text" ? Encoding.UTF8.GetString(payload) : null,
                Hex = Convert.ToHexString(payload),
                Status = "sent",
                Attempts = 1
            });
    }

    public void Dispose()
    {
        lock (this.syncLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            foreach (var writer in this.writers.Values)
            {
                writer.Dispose();
            }

            this.writers.Clear();
        }
    }

    private static string GetDefaultDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            throw new InvalidOperationException("The user profile directory could not be resolved.");
        }

        return Path.Combine(userProfile, "LoRaP2P", "sessions");
    }

    private static bool TryDecodeText(ReadOnlySpan<byte> payload, out string? text)
    {
        try
        {
            text = StrictUtf8.GetString(payload);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = null;
            return false;
        }
    }

    private static string NormalizePayloadType(string payloadType) =>
        payloadType.ToLowerInvariant() switch
        {
            "text" => "text",
            "hex" => "hex",
            _ => throw new ArgumentException("Payload type must be text or hex.", nameof(payloadType))
        };

    private void Append(P2pSessionMessage message)
    {
        message.Validate();
        var json = JsonSerializer.Serialize(message, JsonOptions);

        lock (this.syncLock)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            var writer = this.GetOrCreateWriter(new SessionKey(message.LocalNodeId, message.PeerNodeId));
            writer.WriteLine(json);
            writer.Flush();
        }
    }

    private StreamWriter GetOrCreateWriter(SessionKey key)
    {
        if (this.writers.TryGetValue(key, out var writer))
        {
            return writer;
        }

        Directory.CreateDirectory(this.directory);
        var timestamp = this.timeProvider.GetUtcNow().ToString(
            "yyyyMMdd'T'HHmmssfff'Z'",
            CultureInfo.InvariantCulture);
        var peer = key.PeerNodeId == ushort.MaxValue
            ? "broadcast"
            : key.PeerNodeId.ToString(CultureInfo.InvariantCulture);
        var baseName = $"{timestamp}-node{key.LocalNodeId}-peer{peer}";

        for (var suffix = 0; ; suffix++)
        {
            var suffixText = suffix == 0 ? string.Empty : $"-{suffix}";
            var path = Path.Combine(this.directory, $"{baseName}{suffixText}.jsonl");
            try
            {
                var stream = new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.SequentialScan);
                writer = new StreamWriter(stream, Utf8WithoutBom) { AutoFlush = true };
                this.writers.Add(key, writer);
                return writer;
            }
            catch (IOException) when (File.Exists(path))
            {
            }
        }
    }

    private readonly record struct SessionKey(ushort LocalNodeId, ushort PeerNodeId);
}
