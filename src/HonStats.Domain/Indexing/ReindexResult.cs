namespace HonStats.Domain.Indexing;

public enum ReindexOutcome
{
    Enqueued,
    CooldownRejected,
}

public sealed class ReindexResult
{
    public ReindexOutcome Outcome { get; init; } = ReindexOutcome.CooldownRejected;
    public TimeSpan? RetryAfter { get; init; }
}
