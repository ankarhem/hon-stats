using System.Collections.Concurrent;
using HonStats.App.Insights;

namespace HonStats.Infra.Insights;

internal sealed class IndexProgressTracker : IIndexProgressTracker
{
    private readonly ConcurrentDictionary<Guid, IndexProgress> _state = new();

    public void Start(Guid accountId, int total)
    {
        _state[accountId] = new IndexProgress
        {
            Fetched = 0,
            Total = total,
            Done = false,
            StartedAt = DateTimeOffset.UtcNow,
        };
    }

    public void AddCompleted(Guid accountId, int count)
    {
        _state.AddOrUpdate(
            accountId,
            _ => new IndexProgress
            {
                Fetched = count,
                Total = 0,
                Done = false,
            },
            (_, current) =>
                new IndexProgress
                {
                    Fetched = current.Fetched + count,
                    Total = current.Total,
                    Done = current.Done,
                    StartedAt = current.StartedAt,
                }
        );
    }

    public void Complete(Guid accountId)
    {
        if (_state.TryGetValue(accountId, out var current))
        {
            _state[accountId] = new IndexProgress
            {
                Fetched = current.Fetched,
                Total = current.Total,
                Done = true,
                StartedAt = current.StartedAt,
            };
        }
    }

    public IndexProgress GetProgress(Guid accountId)
    {
        return _state.TryGetValue(accountId, out var p)
            ? p
            : new IndexProgress
            {
                Fetched = 0,
                Total = 0,
                Done = false,
            };
    }
}
