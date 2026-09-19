namespace LoRaP2P.Protocol;

public sealed record P2pOptions
{
    public TimeSpan AckTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public int MaxRetries { get; init; } = 3;

    public TimeSpan BackoffMinimum { get; init; } = TimeSpan.FromMilliseconds(250);

    public TimeSpan BackoffMaximum { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan AckTurnaroundDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    public TimeSpan DuplicateWindow { get; init; } = TimeSpan.FromMinutes(10);

    public int DuplicateEntriesPerPeer { get; init; } = 64;

    public TimeSpan ListenPollTimeout { get; init; } = TimeSpan.FromMilliseconds(500);

    public void Validate()
    {
        ValidatePositive(AckTimeout, nameof(AckTimeout));
        ValidatePositive(BackoffMinimum, nameof(BackoffMinimum));
        ValidatePositive(BackoffMaximum, nameof(BackoffMaximum));
        ValidatePositive(AckTurnaroundDelay, nameof(AckTurnaroundDelay));
        ValidatePositive(DuplicateWindow, nameof(DuplicateWindow));
        ValidatePositive(ListenPollTimeout, nameof(ListenPollTimeout));

        if (BackoffMinimum > BackoffMaximum)
        {
            throw new ArgumentException("BackoffMinimum cannot exceed BackoffMaximum.");
        }

        if (MaxRetries is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetries), "MaxRetries must be between 0 and 10.");
        }

        if (DuplicateEntriesPerPeer is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(DuplicateEntriesPerPeer),
                "DuplicateEntriesPerPeer must be between 1 and 1024.");
        }
    }

    private static void ValidatePositive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The duration must be positive.");
        }
    }
}
