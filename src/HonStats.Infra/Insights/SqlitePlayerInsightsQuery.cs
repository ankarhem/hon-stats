using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Insights;

internal sealed class SqlitePlayerInsightsQuery(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IPlayerInsightsQuery
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
        int limit,
        int offset,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // LEFT JOIN players on TeammateAccountId — names now live in the single
        // source-of-truth players table. players.AccountId is a PK, so each
        // teammate matches at most one row; pagination is unaffected by the join.
        var rows = await (
            from t in db.Teammates
            where t.AccountId == accountId
            orderby t.GamesTogether descending, t.TeammateAccountId
            from p in db.Players.Where(p => p.AccountId == t.TeammateAccountId).DefaultIfEmpty()
            select new TeammateStat
            {
                TeammateAccountId = t.TeammateAccountId,
                DisplayName = p.DisplayName,
                Username = p.Username,
                Country = p.Country,
                GamesTogether = t.GamesTogether,
                WinsTogether = t.WinsTogether,
            }
        )
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

        return rows;
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
                Wins = h.Wins,
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
