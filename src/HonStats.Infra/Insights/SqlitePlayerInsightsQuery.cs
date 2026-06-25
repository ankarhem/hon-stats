using HonStats.App.Insights;
using HonStats.App.Players;
using HonStats.Domain.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Insights;

internal sealed class SqlitePlayerInsightsQuery(
    IDbContextFactory<HonStatsDbContext> dbFactory,
    IPlayerNameResolver names
) : IPlayerInsightsQuery
{
    public async Task<IndexedPlayer?> GetIndexedPlayerAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.IndexedPlayers.FindAsync(new object?[] { accountId }, ct);
    }

    public async Task<IReadOnlyList<TeammateStat>> GetTeammatesAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db
            .Teammates.Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.GamesTogether)
            .ToListAsync(ct);
        if (rows.Count == 0)
            return [];

        var ids = rows.Select(r => r.TeammateAccountId).Distinct().ToList();
        var resolved = await names.ResolveAsync(ids, ct);

        return rows.Select(r =>
            {
                resolved.TryGetValue(r.TeammateAccountId, out var name);
                return new TeammateStat
                {
                    TeammateAccountId = r.TeammateAccountId,
                    DisplayName = name?.DisplayName,
                    Username = name?.Username,
                    Country = name?.Country,
                    GamesTogether = r.GamesTogether,
                    WinsTogether = r.WinsTogether,
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<HeroBuildEntry>> GetHeroBuildAsync(
        Guid accountId,
        int heroId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db
            .HeroBuilds.Where(h => h.AccountId == accountId && h.HeroId == heroId)
            .OrderByDescending(h => h.Frequency)
            .ToListAsync(ct);

        return rows.Select(h => new HeroBuildEntry
            {
                ItemId = h.ItemId,
                Frequency = h.Frequency,
                Games = h.Games,
            })
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, int>> GetHeroGamesAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.PlayerMatches.Where(p => p.AccountId == accountId).ToListAsync(ct);
        return rows.GroupBy(r => r.HeroId).ToDictionary(g => g.Key, g => g.Count());
    }
}
