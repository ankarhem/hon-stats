using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Insights;

internal sealed class SqlitePlayerInsightsQuery(
    IDbContextFactory<HonStatsDbContext> dbFactory,
    IInsightsRawQuery rawQuery
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

    public async Task<IReadOnlyList<PlayerMatch>> GetRecentMatchesAsync(
        Guid accountId,
        int limit,
        int offset,
        string? map = null,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.PlayerMatches.Where(p => p.AccountId == accountId).ToListAsync(ct);

        // Filter + order in memory: SQLite's EF provider can't order by DateTimeOffset
        // (Date) server-side, and the map store has no server-side map param.
        IEnumerable<PlayerMatch> q = rows;
        if (ShouldFilterByMap(map))
            q = q.Where(r => string.Equals(r.Map, map, StringComparison.OrdinalIgnoreCase));
        return q.OrderByDescending(r => r.Date).Skip(offset).Take(limit).ToList();
    }

    public async Task<IReadOnlyList<TeammateStat>> GetTeammatesAsync(
        Guid accountId,
        int limit,
        int offset,
        string? map = null,
        CancellationToken ct = default
    )
    {
        if (ShouldFilterByMap(map))
        {
            var inputs = await rawQuery.GetTeammateInputsAsync(accountId, map, ct);
            var aggregates = TeammateAggregator.Build(inputs);
            var paged = aggregates.Skip(offset).Take(limit).ToList();

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var teammateIds = paged.Select(a => a.TeammateAccountId).ToList();
            var names = await db
                .Players.Where(p => teammateIds.Contains(p.AccountId))
                .ToDictionaryAsync(p => p.AccountId, ct);

            return paged
                .Select(a =>
                {
                    names.TryGetValue(a.TeammateAccountId, out var p);
                    return new TeammateStat
                    {
                        TeammateAccountId = a.TeammateAccountId,
                        DisplayName = p?.DisplayName,
                        Username = p?.Username,
                        Country = p?.Country,
                        GamesTogether = a.GamesTogether,
                        WinsTogether = a.WinsTogether,
                    };
                })
                .ToList();
        }

        await using var db2 = await dbFactory.CreateDbContextAsync(ct);

        // LEFT JOIN players on TeammateAccountId — names now live in the single
        // source-of-truth players table. players.AccountId is a PK, so each
        // teammate matches at most one row; pagination is unaffected by the join.
        var rows = await (
            from t in db2.Teammates
            where t.AccountId == accountId
            orderby t.GamesTogether descending, t.TeammateAccountId
            from p in db2.Players.Where(p => p.AccountId == t.TeammateAccountId).DefaultIfEmpty()
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
        string? map = null,
        CancellationToken ct = default
    )
    {
        if (ShouldFilterByMap(map))
        {
            var inputs = await rawQuery.GetHeroItemInputsAsync(accountId, heroId, map, ct);
            return HeroBuildAggregator.Build(inputs);
        }

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
        string? map = null,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.PlayerMatches.Where(p => p.AccountId == accountId).ToListAsync(ct);
        if (ShouldFilterByMap(map))
            rows = rows.Where(r => string.Equals(r.Map, map, StringComparison.OrdinalIgnoreCase))
                .ToList();
        return rows.GroupBy(r => r.HeroId).ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<IReadOnlyList<ItemTimingEntry>> GetHeroItemTimingAsync(
        Guid accountId,
        int heroId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // match_item_timing has no HeroId; scope to this hero's games via the games
        // the player played as that hero in match_roster. Each (GameId, ItemId)
        // is unique per account, so the mean is naturally over distinct games.
        var heroGames = db
            .MatchRoster.Where(r => r.AccountId == accountId && r.HeroId == heroId)
            .Select(r => r.GameId)
            .Distinct();

        return await (
            from mit in db.MatchItemTimings
            where mit.AccountId == accountId && heroGames.Contains(mit.GameId)
            group mit by mit.ItemId into g
            select new ItemTimingEntry
            {
                ItemId = g.Key,
                AvgSeconds = g.Average(x => (double)x.FirstSeenSeconds),
                Games = g.Count(),
            }
        ).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MapStatEntry>> GetMapStatsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var inputs = await GetStatInputsAsync(accountId, ct);
        return MapStatsAggregator.Build(inputs);
    }

    public async Task<MapStatEntry> GetOverallStatsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var inputs = await GetStatInputsAsync(accountId, ct);
        return MapStatsAggregator.BuildOverall(inputs);
    }

    private async Task<IReadOnlyList<MatchStatInput>> GetStatInputsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var matches = await db.PlayerMatches.Where(m => m.AccountId == accountId).ToListAsync(ct);
        if (matches.Count == 0)
            return [];

        var gameIds = matches.Select(m => m.GameId).Distinct().ToList();

        // Won + the gold breakdown live on the subject's match_roster row (one per
        // game). Keyed by GameId for a per-match lookup as the aggregator consumes.
        var rosterByGame = await db
            .MatchRoster.Where(r => r.AccountId == accountId && gameIds.Contains(r.GameId))
            .ToDictionaryAsync(r => r.GameId, ct);

        return matches
            .Select(m =>
            {
                rosterByGame.TryGetValue(m.GameId, out var r);
                var goldEarned = GoldEarnedFor(r);
                return new MatchStatInput
                {
                    GameId = m.GameId,
                    Map = m.Map,
                    Kills = m.Kills,
                    Deaths = m.Deaths,
                    Assists = m.Assists,
                    GoldEarned = goldEarned,
                    Experience = r?.Experience,
                    HeroDamage = r?.HeroDamage,
                    DurationSeconds = m.Duration,
                    Won = r?.Won ?? false,
                    WardsPlaced = r?.WardOfSightPlaced ?? 0,
                };
            })
            .ToList();
    }

    // GoldEarned sums the five earned-gold sources; null when any is absent
    // (pre-gold-ingestion roster rows stay null so GPM degrades gracefully).
    private static int? GoldEarnedFor(MatchRoster? r)
    {
        if (
            r is null
            || !r.GoldFromCreeps.HasValue
            || !r.GoldFromNeutrals.HasValue
            || !r.GoldFromKills.HasValue
            || !r.GoldFromAssists.HasValue
            || !r.GoldFromBuildings.HasValue
        )
        {
            return null;
        }

        return r.GoldFromCreeps.Value
            + r.GoldFromNeutrals.Value
            + r.GoldFromKills.Value
            + r.GoldFromAssists.Value
            + r.GoldFromBuildings.Value;
    }

    private static bool ShouldFilterByMap(string? map) =>
        !string.IsNullOrWhiteSpace(map)
        && !string.Equals(map, "all", StringComparison.OrdinalIgnoreCase);
}
