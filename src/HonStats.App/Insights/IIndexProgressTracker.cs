namespace HonStats.App.Insights;

public sealed class IndexProgress
{
    public int Fetched { get; init; }
    public int Total { get; init; }
    public bool Done { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
}

public interface IIndexProgressTracker
{
    void Start(Guid accountId, int total);

    void AddCompleted(Guid accountId, int count);

    void Complete(Guid accountId);

    IndexProgress GetProgress(Guid accountId);
}
