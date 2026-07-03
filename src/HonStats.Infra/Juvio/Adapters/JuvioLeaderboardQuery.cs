using System.Text.Json;
using HonStats.App.Leaderboard;
using HonStats.App.Players;
using Microsoft.Extensions.Caching.Memory;

namespace HonStats.Infra.Juvio.Adapters;

// seasonId=1 is pinned in the URL: omitting it yields a different/larger board
// than seasonId=1 (documented gotcha — see AGENTS.md "Leaderboard endpoints").
internal sealed class JuvioLeaderboardQuery(
    IHttpClientFactory httpClientFactory,
    IMemoryCache cache,
    IPlayerNameResolver nameResolver
) : ILeaderboardQuery
{
    public async Task<IReadOnlyList<LeaderboardEntry>> GetAsync(
        string map,
        CancellationToken ct = default
    )
    {
        return (
                await cache.GetOrCreateAsync(
                    $"juvio:leaderboard:{map}",
                    async _ =>
                    {
                        var dto = await GetAsync<LeaderboardDto>(
                            $"/v1/stats/getleaderboard?map={Uri.EscapeDataString(map)}&seasonId=1",
                            ct
                        );
                        if (dto is null)
                            return new List<LeaderboardEntry>();

                        var entries = dto
                            .TopPlayersValue.Select(Map)
                            .Where(e => e != null)
                            .Select(e => e!)
                            .ToList();
                        if (entries.Count == 0)
                            return entries;

                        // Entries carry accountId + displayName but NOT username, which
                        // the /players/{username} profile route needs. Resolve in bulk
                        // (chunks ≤50 internally) and merge; fall back to displayName.
                        var resolved = await nameResolver.ResolveAsync(
                            entries.Select(e => e.AccountId).ToList(),
                            ct
                        );
                        return entries
                            .Select(e =>
                            {
                                if (
                                    resolved.TryGetValue(e.AccountId, out var name)
                                    && !string.IsNullOrWhiteSpace(name.Username)
                                )
                                {
                                    return e with { Username = name.Username };
                                }
                                return e;
                            })
                            .ToList();
                    },
                    new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                    }
                )
            ) ?? [];
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, JuvioJson.Options, ct);
    }

    private static LeaderboardEntry? Map(LeaderboardPlayerDto p)
    {
        if (!Guid.TryParse(p.AccountId, out var accountId))
            return null;
        return new LeaderboardEntry(
            p.Rank,
            accountId,
            p.DisplayName,
            p.DisplayName,
            p.Mmr,
            p.Country,
            p.RankName,
            p.RankIcon,
            p.StarLevel,
            p.Color,
            p.Avatar
        );
    }
}
