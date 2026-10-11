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
        ValidatePositive(this.AckTimeout, nameof(this.AckTimeout));
        ValidatePositive(this.BackoffMinimum, nameof(this.BackoffMinimum));
        ValidatePositive(this.BackoffMaximum, nameof(this.BackoffMaximum));
        ValidatePositive(this.AckTurnaroundDelay, nameof(this.AckTurnaroundDelay));
        ValidatePositive(this.DuplicateWindow, nameof(this.DuplicateWindow));
        ValidatePositive(this.ListenPollTimeout, nameof(this.ListenPollTimeout));

        if (this.BackoffMinimum > this.BackoffMaximum)
        {
            throw new ArgumentException("BackoffMinimum cannot exceed BackoffMaximum.");
        }

        if (this.MaxRetries is < 0 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(this.MaxRetries), "MaxRetries must be between 0 and 10.");
        }

        if (this.DuplicateEntriesPerPeer is < 1 or > 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(this.DuplicateEntriesPerPeer),
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
