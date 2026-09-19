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
        if (Timestamp == default)
        {
            throw new InvalidDataException("Session message timestamp is missing.");
        }

        if (Direction is not ("incoming" or "outgoing" or "broadcast"))
        {
            throw new InvalidDataException($"Unknown session message direction '{Direction}'.");
        }

        if (LocalNodeId is 0 or ushort.MaxValue)
        {
            throw new InvalidDataException($"Invalid local node ID {LocalNodeId}.");
        }

        var isBroadcastPeer = PeerNodeId == ushort.MaxValue;
        if (PeerNodeId == 0 || (Direction == "broadcast") != isBroadcastPeer)
        {
            throw new InvalidDataException($"Invalid peer node ID {PeerNodeId}.");
        }

        if (PayloadType is not ("text" or "hex"))
        {
            throw new InvalidDataException($"Unknown payload type '{PayloadType}'.");
        }

        if (PayloadType == "text" && Text is null)
        {
            throw new InvalidDataException("A text session message requires text.");
        }

        if (string.IsNullOrWhiteSpace(Hex) || Hex.Length % 2 != 0)
        {
            throw new InvalidDataException("Session message hex payload is invalid.");
        }

        try
        {
            _ = Convert.FromHexString(Hex);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("Session message hex payload is invalid.", exception);
        }

        if (Status is not ("received" or "acknowledged" or "failed" or "sent"))
        {
            throw new InvalidDataException($"Unknown session message status '{Status}'.");
        }

        if (Attempts is <= 0)
        {
            throw new InvalidDataException("Session message attempts must be positive.");
        }
    }
}
