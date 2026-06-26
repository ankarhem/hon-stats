using HonStats.App.Players;
using Microsoft.Extensions.Caching.Memory;

namespace HonStats.Infra.Juvio.Adapters;

// Caches player identity lookups (getuserinfo) per accountId. Player names and
// countries essentially never change, so a long TTL eliminates redundant API
// calls during indexing, match detail rendering, and profile loads.
internal sealed class CachedPlayerNameResolver(IPlayerNameResolver inner, IMemoryCache cache)
    : IPlayerNameResolver
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public async Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default
    )
    {
        var result = new Dictionary<Guid, ResolvedName>();
        var uncached = new List<Guid>();

        foreach (var id in accountIds.Distinct())
        {
            if (cache.TryGetValue<ResolvedName>(id, out var cached) && cached is not null)
                result[id] = cached;
            else
                uncached.Add(id);
        }

        if (uncached.Count == 0)
            return result;

        var fetched = await inner.ResolveAsync(uncached, ct);
        foreach (var (id, name) in fetched)
        {
            cache.Set(id, name, Ttl);
            result[id] = name;
        }

        return result;
    }
}
