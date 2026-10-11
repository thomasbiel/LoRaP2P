namespace LoRaP2P.Console;

internal sealed record P2pSessionMessage
{
    public required DateTimeOffset Timestamp { get; init; }

    public required string Direction { get; init; }

    public required ushort LocalNodeId { get; init; }

    public required ushort PeerNodeId { get; init; }

    public required uint SequenceNumber { get; init; }

    public required string PayloadType { get; init; }

    public string? Text { get; init; }

    public required string Hex { get; init; }

    public required string Status { get; init; }

    public int? Attempts { get; init; }

    public int? RssiDbm { get; init; }

    public double? SnrDb { get; init; }

    public void Validate()
    {
        if (this.Timestamp == default)
        {
            throw new InvalidDataException("Session message timestamp is missing.");
        }

        if (this.Direction is not ("incoming" or "outgoing" or "broadcast"))
        {
            throw new InvalidDataException($"Unknown session message direction '{this.Direction}'.");
        }

        if (this.LocalNodeId is 0 or ushort.MaxValue)
        {
            throw new InvalidDataException($"Invalid local node ID {this.LocalNodeId}.");
        }

        var isBroadcastPeer = this.PeerNodeId == ushort.MaxValue;
        if (this.PeerNodeId == 0 || (this.Direction == "broadcast") != isBroadcastPeer)
        {
            throw new InvalidDataException($"Invalid peer node ID {this.PeerNodeId}.");
        }

        if (this.PayloadType is not ("text" or "hex"))
        {
            throw new InvalidDataException($"Unknown payload type '{this.PayloadType}'.");
        }

        if (this.PayloadType == "text" && this.Text is null)
        {
            throw new InvalidDataException("A text session message requires text.");
        }

        if (string.IsNullOrWhiteSpace(this.Hex) || this.Hex.Length % 2 != 0)
        {
            throw new InvalidDataException("Session message hex payload is invalid.");
        }

        try
        {
            _ = Convert.FromHexString(this.Hex);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Session message hex payload is invalid.", exception);
        }

        if (this.Status is not ("received" or "acknowledged" or "failed" or "sent"))
        {
            throw new InvalidDataException($"Unknown session message status '{this.Status}'.");
        }

        if (this.Attempts is <= 0)
        {
            throw new InvalidDataException("Session message attempts must be positive.");
        }
    }
}
