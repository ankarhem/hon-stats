using HonStats.App.Events;
using HonStats.Domain.Events;

namespace HonStats.Infra.PlayerSearch;

// Flips the cache stale flag whenever a player's matches are indexed; the seed
// service's poll loop notices within one PollInterval and rebuilds. This decouples the
// per-player event rate from the rebuild cost — a burst of PlayerMatchesIndexed events
// during bulk indexing collapses into a single rebuild per PollInterval.
internal sealed class PlayerNameIndexRefreshHandler(PlayerNameIndexCache cache)
    : IEventHandler<PlayerMatchesIndexed>
{
    public Task HandleAsync(PlayerMatchesIndexed @event, CancellationToken ct = default)
    {
        cache.MarkStale();
        return Task.CompletedTask;
    }
}
