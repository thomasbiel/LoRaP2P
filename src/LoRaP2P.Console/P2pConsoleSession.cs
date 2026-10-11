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

    private readonly ILoraRadio radio;
    private readonly P2pConfigurationStore store;
    private readonly P2pSessionLog sessionLog;
    private readonly IRadioBonnetDisplay? display;
    private P2pNode node;

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

        this.radio = radio;
        this.store = store;
        this.sessionLog = sessionLog ?? new P2pSessionLog();
        this.display = display;
        this.Configuration = configuration;
        this.node = this.CreateNode(configuration);
        this.node.DataReceived += this.HandleDataReceived;
    }

    public P2pConfiguration Configuration { get; private set; }

    public P2pNode Node => this.node;

    public string SessionDirectory => this.sessionLog.DirectoryPath;

    public async Task<P2pSendResult> SendAsync(
        P2pNodeId peer,
        ReadOnlyMemory<byte> payload,
        string payloadType,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await this.node.SendAsync(peer, payload, cancellationToken);
            this.sessionLog.AppendSent(
                this.node.LocalNodeId,
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
            this.sessionLog.AppendSent(
                this.node.LocalNodeId,
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
        var result = await this.node.BroadcastAsync(payload, cancellationToken);
        this.sessionLog.AppendBroadcast(
            this.node.LocalNodeId,
            result.SequenceNumber,
            payload.Span,
            payloadType);
        return result;
    }

    public async Task SetLocalNodeIdAsync(
        P2pNodeId localNodeId,
        CancellationToken cancellationToken)
    {
        var updated = this.Configuration with { LocalNodeId = localNodeId };
        updated.Validate();
        await this.store.SaveAsync(updated, cancellationToken);

        var previous = this.node;
        previous.DataReceived -= this.HandleDataReceived;
        this.Configuration = updated;
        this.node = this.CreateNode(updated);
        this.node.DataReceived += this.HandleDataReceived;
        previous.Dispose();
    }

    public async Task<bool> AddPeerAsync(
        P2pNodeId peer,
        CancellationToken cancellationToken)
    {
        if (peer == this.Configuration.LocalNodeId)
        {
            throw new ArgumentException("The local node ID cannot be added as a peer.", nameof(peer));
        }

        if (this.node.Peers.Contains(peer))
        {
            return false;
        }

        var peers = this.Configuration.Peers.Append(peer).OrderBy(static id => id.Value).ToArray();
        var updated = this.Configuration with { Peers = peers };
        updated.Validate();
        await this.store.SaveAsync(updated, cancellationToken);

        this.Configuration = updated;
        this.node.Peers.Add(peer);
        return true;
    }

    public void Dispose()
    {
        this.node.DataReceived -= this.HandleDataReceived;
        this.node.Dispose();
        this.sessionLog.Dispose();
    }

    private void HandleDataReceived(P2pReceivedMessage message)
    {
        this.sessionLog.AppendReceived(this.node.LocalNodeId, message);
        if (message.Recipient.IsBroadcast && this.display is not null)
        {
            this.display.ShowBroadcastMessage(GetDisplayText(message.Payload));
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
            this.radio,
            configuration.LocalNodeId,
            configuration.Options,
            new PeerRegistry(configuration.Peers));
}
