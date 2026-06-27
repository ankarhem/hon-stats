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

    public async Task<IReadOnlyList<HeroItemPairEntry>> GetHeroItemPairsAsync(
        Guid accountId,
        int heroId,
        int limit,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db
            .HeroItemPairs.Where(h => h.AccountId == accountId && h.HeroId == heroId)
            .OrderByDescending(h => h.Frequency)
            .ThenBy(h => h.ItemA)
            .ThenBy(h => h.ItemB)
            .Take(limit)
            .ToListAsync(ct);

        return rows.Select(h => new HeroItemPairEntry
            {
                ItemA = h.ItemA,
                ItemB = h.ItemB,
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

    public async Task<IReadOnlyList<ItemTimingEntry>> GetHeroItemTimingAsync(
        Guid accountId,
        int heroId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // match_item_timing has no HeroId; scope to this hero's games via the games
        // the player played as that hero in match_player_items. Each (GameId, ItemId)
        // is unique per account, so the mean is naturally over distinct games.
        var heroGames = db
            .MatchPlayerItems.Where(mpi => mpi.AccountId == accountId && mpi.HeroId == heroId)
            .Select(mpi => mpi.GameId)
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

        // WardsPlaced lives on match_player_items, duplicated across every inventory
        // slot row for a (game, player) — so take Max per GameId (the rows are
        // identical; Sum would over-count by slot count). Absent for games whose
        // player has no item rows → treated as 0 by the lookup fallback.
        var wardsByGame = await (
            from mpi in db.MatchPlayerItems
            where mpi.AccountId == accountId && gameIds.Contains(mpi.GameId)
            group mpi by mpi.GameId into g
            select new { GameId = g.Key, Wards = g.Max(x => x.WardsPlaced) }
        ).ToDictionaryAsync(x => x.GameId, x => x.Wards, ct);

        var inputs = matches
            .Select(m =>
            {
                rosterByGame.TryGetValue(m.GameId, out var r);
                wardsByGame.TryGetValue(m.GameId, out var wards);
                var goldEarned = GoldEarnedFor(r);
                return new MatchStatInput
                {
                    GameId = m.GameId,
                    Map = m.Map,
                    Kills = m.Kills,
                    Deaths = m.Deaths,
                    Assists = m.Assists,
                    GoldEarned = goldEarned,
                    DurationSeconds = m.Duration,
                    Won = r?.Won ?? false,
                    WardsPlaced = wards,
                };
            })
            .ToList();

        return MapStatsAggregator.Build(inputs);
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
}
