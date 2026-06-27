using HonStats.App.Players;
using HonStats.Domain.Players;

namespace HonStats.Infra.PlayerSearch;

// IPlayerNameSearch adapter over the singleton PlayerNameIndexCache. Synchronous under
// the hood — the port is async (Task-returning, CancellationToken-aware) to allow a
// future FTS5 adapter that queries the database per call without changing call sites.
internal sealed class CachedPlayerNameSearch(PlayerNameIndexCache cache) : IPlayerNameSearch
{
    public Task<IReadOnlyList<PlayerNameSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken ct = default
    )
    {
        return Task.FromResult(cache.Search(query, limit));
    }
}
