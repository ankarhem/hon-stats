using HonStats.App.Players;
using HonStats.Domain.Players;
using Microsoft.Extensions.Caching.Memory;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class CachedPlayerSearch(IPlayerSearch inner, IMemoryCache cache) : IPlayerSearch
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(1);

    public async Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(
        string username,
        CancellationToken ct = default
    )
    {
        var key = $"search:{username.Trim().ToLowerInvariant()}";
        if (
            cache.TryGetValue<IReadOnlyList<PlayerSearchResult>>(key, out var cached)
            && cached is not null
        )
            return cached;

        var result = await inner.SearchAsync(username, ct);
        // Don't cache misses: a transient juvio empty-200 would poison this key for the TTL.
        if (result.Count > 0)
            cache.Set(key, result, Ttl);
        return result;
    }
}
