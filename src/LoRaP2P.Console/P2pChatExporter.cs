using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace LoRaP2P.Console;

internal static class P2pChatExporter
{
    private const string TemplateResourceName = "LoRaP2P.Console.Resources.P2pChatTemplate.html";
    private const string MessageTemplateBegin = "{{MESSAGE_TEMPLATE_BEGIN}}";
    private const string MessageTemplateEnd = "{{MESSAGE_TEMPLATE_END}}";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Lazy<TemplateParts> Templates = new(LoadTemplates);

    public static async Task<string> ExportAsync(
        string inputPath,
        string? outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        var input = Path.GetFullPath(inputPath);
        var output = Path.GetFullPath(outputPath ?? Path.ChangeExtension(input, ".html"));
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        if (comparer.Equals(input, output))
        {
            throw new ArgumentException("Input and output paths must be different.", nameof(outputPath));
        }

        var messages = await ReadMessagesAsync(input, cancellationToken);
        var html = BuildHtml(input, messages);
        var outputDirectory = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(outputDirectory);
        var temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(output)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                html,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            
            File.Move(temporaryPath, output, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return output;
    }

    private static async Task<IReadOnlyList<P2pSessionMessage>> ReadMessagesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        List<P2pSessionMessage> messages = [];
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var message = JsonSerializer.Deserialize<P2pSessionMessage>(line, JsonOptions)
                    ?? throw new JsonException("The JSON object is null.");
                message.Validate();
                messages.Add(message);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                throw new InvalidDataException(
                    $"Session file '{path}' contains an invalid message at line {lineNumber}: {exception.Message}",
                    exception);
            }
        }

        if (messages.Count == 0)
        {
            throw new InvalidDataException($"Session file '{path}' contains no messages.");
        }

        return messages;
    }

    private static string BuildHtml(string inputPath, IReadOnlyList<P2pSessionMessage> messages)
    {
        var first = messages[0];
        var peer = first.PeerNodeId == ushort.MaxValue ? "Broadcast" : $"Peer {first.PeerNodeId}";
        var title = $"LoRaP2P Chat - Node {first.LocalNodeId} / {peer}";
        StringBuilder messageHtml = new();

        foreach (var message in messages)
        {
            var cssClass = message.Direction;
            if (message.Status == "failed")
            {
                cssClass += " failed";
            }

            var sender = message.Direction == "incoming"
                ? $"Peer {message.PeerNodeId}"
                : message.Direction == "broadcast" ? "Ich an alle" : "Ich";
            
            var content = message.Text ?? "(Binärnachricht)";
            StringBuilder metadata = new();
            metadata.Append("Seq ").Append(message.SequenceNumber.ToString(CultureInfo.InvariantCulture))
                .Append(" · ").Append(message.Status)
                .Append(" · ").Append(message.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture));

            if (message.Attempts is not null)
            {
                metadata.Append(" · ").Append(message.Attempts.Value.ToString(CultureInfo.InvariantCulture)).Append(" Versuch(e)");
            }

            if (message.RssiDbm is not null)
            {
                metadata.Append(" · RSSI ").Append(message.RssiDbm.Value.ToString(CultureInfo.InvariantCulture))
                    .Append(" dBm · SNR ").Append(message.SnrDb?.ToString("F2", CultureInfo.InvariantCulture));
            }

            messageHtml.Append(RenderTemplate(
                Templates.Value.Message,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["CSS_CLASS"] = WebUtility.HtmlEncode(cssClass),
                    ["SENDER"] = WebUtility.HtmlEncode(sender),
                    ["CONTENT"] = WebUtility.HtmlEncode(content),
                    ["HEX"] = WebUtility.HtmlEncode(message.Hex),
                    ["META"] = WebUtility.HtmlEncode(metadata.ToString())
                }));
        }

        return RenderTemplate(
            Templates.Value.Document,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["TITLE"] = WebUtility.HtmlEncode(title),
                ["SOURCE_FILE"] = WebUtility.HtmlEncode(Path.GetFileName(inputPath)),
                ["MESSAGES"] = messageHtml.ToString()
            });
    }

    private static TemplateParts LoadTemplates()
    {
        using var stream = typeof(P2pChatExporter).Assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded chat template '{TemplateResourceName}' was not found.");

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var template = reader.ReadToEnd();
        var messageStart = template.IndexOf(MessageTemplateBegin, StringComparison.Ordinal);
        var messageEnd = template.IndexOf(MessageTemplateEnd, StringComparison.Ordinal);
        if (messageStart < 0 || messageEnd <= messageStart)
        {
            throw new InvalidOperationException("The embedded chat template has invalid message markers.");
        }

        var messageTemplateStart = messageStart + MessageTemplateBegin.Length;
        var document = string.Concat(
            template.AsSpan(0, messageStart),
            template.AsSpan(messageEnd + MessageTemplateEnd.Length));

        var message = template[messageTemplateStart..messageEnd];
        return new TemplateParts(document, message);
    }

    private static string RenderTemplate(string template, Dictionary<string, string> values)
    {
        StringBuilder result = new(template.Length);
        var position = 0;
        while (position < template.Length)
        {
            var tokenStart = template.IndexOf("{{", position, StringComparison.Ordinal);
            if (tokenStart < 0)
            {
                result.Append(template, position, template.Length - position);
                break;
            }

            result.Append(template, position, tokenStart - position);
            var tokenEnd = template.IndexOf("}}", tokenStart + 2, StringComparison.Ordinal);
            if (tokenEnd < 0)
            {
                throw new InvalidOperationException("The embedded chat template contains an unterminated placeholder.");
            }

            var key = template[(tokenStart + 2)..tokenEnd];
            if (!values.TryGetValue(key, out var value))
            {
                throw new InvalidOperationException(
                    $"The embedded chat template contains the unknown placeholder '{{{{{key}}}}}'.");
            }

            result.Append(value);
            position = tokenEnd + 2;
        }

        return result.ToString();
    }

    private sealed record TemplateParts(string Document, string Message);
}
