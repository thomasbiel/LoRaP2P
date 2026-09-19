using System.Text;
using LoRaP2P.Protocol;
using LoRaP2P.Protocol.Frames;
using LoRaP2P.Protocol.Peers;
using LoRaP2P.Radio.Rfm9x;

namespace LoRaP2P.Console;

internal sealed class P2pConsoleSession : IDisposable
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly ILoraRadio _radio;
    private readonly P2pConfigurationStore _store;
    private readonly P2pSessionLog _sessionLog;
    private readonly IRadioBonnetDisplay? _display;
    private P2pNode _node;

    public P2pConsoleSession(
        ILoraRadio radio,
        P2pConfiguration configuration,
        P2pConfigurationStore store,
        P2pSessionLog? sessionLog = null,
        IRadioBonnetDisplay? display = null)
    {
        ArgumentNullException.ThrowIfNull(radio);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(store);
        configuration.Validate();

        _radio = radio;
        _store = store;
        _sessionLog = sessionLog ?? new P2pSessionLog();
        _display = display;
        Configuration = configuration;
        _node = CreateNode(configuration);
        _node.DataReceived += HandleDataReceived;
    }

    public P2pConfiguration Configuration { get; private set; }

    public P2pNode Node => _node;

    public string SessionDirectory => _sessionLog.DirectoryPath;

    public async Task<P2pSendResult> SendAsync(
        P2pNodeId peer,
        ReadOnlyMemory<byte> payload,
        string payloadType,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _node.SendAsync(peer, payload, cancellationToken);
            _sessionLog.AppendSent(
                _node.LocalNodeId,
                peer,
                result.SequenceNumber,
                payload.Span,
                payloadType,
                "acknowledged",
                result.Attempts);
            return result;
        }
        catch (P2pDeliveryException exception)
        {
            _sessionLog.AppendSent(
                _node.LocalNodeId,
                peer,
                exception.SequenceNumber,
                payload.Span,
                payloadType,
                "failed",
                exception.Attempts);
            throw;
        }
    }

    public async Task<P2pBroadcastResult> BroadcastAsync(
        ReadOnlyMemory<byte> payload,
        string payloadType,
        CancellationToken cancellationToken)
    {
        var result = await _node.BroadcastAsync(payload, cancellationToken);
        _sessionLog.AppendBroadcast(
            _node.LocalNodeId,
            result.SequenceNumber,
            payload.Span,
            payloadType);
        return result;
    }

    public async Task SetLocalNodeIdAsync(
        P2pNodeId localNodeId,
        CancellationToken cancellationToken)
    {
        var updated = Configuration with { LocalNodeId = localNodeId };
        updated.Validate();
        await _store.SaveAsync(updated, cancellationToken);

        var previous = _node;
        previous.DataReceived -= HandleDataReceived;
        Configuration = updated;
        _node = CreateNode(updated);
        _node.DataReceived += HandleDataReceived;
        previous.Dispose();
    }

    public async Task<bool> AddPeerAsync(
        P2pNodeId peer,
        CancellationToken cancellationToken)
    {
        if (peer == Configuration.LocalNodeId)
        {
            throw new ArgumentException("The local node ID cannot be added as a peer.", nameof(peer));
        }

        if (_node.Peers.Contains(peer))
        {
            return false;
        }

        var peers = Configuration.Peers.Append(peer).OrderBy(static id => id.Value).ToArray();
        var updated = Configuration with { Peers = peers };
        updated.Validate();
        await _store.SaveAsync(updated, cancellationToken);

        Configuration = updated;
        _node.Peers.Add(peer);
        return true;
    }

    public void Dispose()
    {
        _node.DataReceived -= HandleDataReceived;
        _node.Dispose();
        _sessionLog.Dispose();
    }

    private void HandleDataReceived(P2pReceivedMessage message)
    {
        _sessionLog.AppendReceived(_node.LocalNodeId, message);
        if (message.Recipient.IsBroadcast && _display is not null)
        {
            _display.ShowBroadcastMessage(GetDisplayText(message.Payload));
        }
    }

    private static string GetDisplayText(byte[] payload)
    {
        try
        {
            return StrictUtf8.GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return Convert.ToHexString(payload);
        }
    }

    private P2pNode CreateNode(P2pConfiguration configuration) =>
        new(
            _radio,
            configuration.LocalNodeId,
            configuration.Options,
            new PeerRegistry(configuration.Peers));
}
