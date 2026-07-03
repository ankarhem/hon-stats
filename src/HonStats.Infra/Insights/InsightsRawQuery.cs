using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Insights;

internal sealed class InsightsRawQuery(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IInsightsRawQuery
{
    public async Task<IReadOnlyList<int>> GetHeroIdsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db
            .MatchRoster.Where(r => r.AccountId == accountId && r.HeroId != null)
            .Select(r => r.HeroId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MatchItemInput>> GetHeroItemInputsAsync(
        Guid accountId,
        int heroId,
        string? map = null,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var heroGames = await db
            .MatchRoster.Where(r => r.AccountId == accountId && r.HeroId == heroId)
            .ToListAsync(ct);
        if (heroGames.Count == 0)
            return [];

        var wonByGame = heroGames.ToDictionary(r => r.GameId, r => r.Won);
        var gameIds = heroGames.Select(r => r.GameId).Distinct().ToList();

        // Map lives on the subject's player_matches row (only the aggregated
        // account has one).
        var mapByGame = await db
            .PlayerMatches.Where(m => m.AccountId == accountId && gameIds.Contains(m.GameId))
            .ToDictionaryAsync(m => m.GameId, m => m.Map, ct);

        // Per-map filter: narrow to only games played on the requested map.
        // "all" and null are treated identically (no filtering).
        if (ShouldFilterByMap(map))
        {
            var matchingGameIds = MatchingGameIds(mapByGame, map!);
            gameIds = gameIds.Where(g => matchingGameIds.Contains(g)).ToList();
            if (gameIds.Count == 0)
                return [];
        }

        var rows = await db
            .MatchPlayerItems.Where(i => i.AccountId == accountId && gameIds.Contains(i.GameId))
            .ToListAsync(ct);

        return rows.GroupBy(r => r.GameId)
            .Select(g => new MatchItemInput
            {
                GameId = g.Key,
                Won = wonByGame.TryGetValue(g.Key, out var won) && won,
                Map = mapByGame.TryGetValue(g.Key, out var m) ? m : string.Empty,
                ItemIds = g.Select(x => x.ItemId).ToList(),
            })
            .ToList();
    }

    public async Task<IReadOnlyList<MatchTeammateInput>> GetTeammateInputsAsync(
        Guid accountId,
        string? map = null,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var subjectGames = await db
            .MatchRoster.Where(r => r.AccountId == accountId)
            .ToListAsync(ct);
        if (subjectGames.Count == 0)
            return [];

        // Per-map filter: narrow the subject's games to only those on the
        // requested map before joining for teammates.
        if (ShouldFilterByMap(map))
        {
            var subjectGameIds = subjectGames.Select(g => g.GameId).Distinct().ToList();
            var matchingGameIds = await db
                .PlayerMatches.Where(m =>
                    m.AccountId == accountId && subjectGameIds.Contains(m.GameId) && m.Map == map
                )
                .Select(m => m.GameId)
                .ToHashSetAsync(ct);
            subjectGames = subjectGames.Where(g => matchingGameIds.Contains(g.GameId)).ToList();
            if (subjectGames.Count == 0)
                return [];
        }

        var gameIds = subjectGames.Select(g => g.GameId).Distinct().ToList();
        var allForGames = await db
            .MatchRoster.Where(r => gameIds.Contains(r.GameId))
            .ToListAsync(ct);

        return subjectGames
            .Select(g => new MatchTeammateInput
            {
                GameId = g.GameId,
                Won = g.Won,
                TeammateAccountIds = allForGames
                    .Where(r =>
                        r.GameId == g.GameId && r.Team == g.Team && r.AccountId != accountId
                    )
                    .Select(r => r.AccountId)
                    .ToList(),
            })
            .ToList();
    }

    // "all" and null/empty mean "no map filter" — the caller wants every game.
    public async Task<IReadOnlyList<MatchItemInput>> GetHeroItemsBoughtAsync(
        Guid accountId,
        int heroId,
        string? map = null,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var heroGames = await db
            .MatchRoster.Where(r => r.AccountId == accountId && r.HeroId == heroId)
            .ToListAsync(ct);
        if (heroGames.Count == 0)
            return [];

        var wonByGame = heroGames.ToDictionary(r => r.GameId, r => r.Won);
        var gameIds = heroGames.Select(r => r.GameId).Distinct().ToList();

        var mapByGame = await db
            .PlayerMatches.Where(m => m.AccountId == accountId && gameIds.Contains(m.GameId))
            .ToDictionaryAsync(m => m.GameId, m => m.Map, ct);

        if (ShouldFilterByMap(map))
        {
            var matchingGameIds = MatchingGameIds(mapByGame, map!);
            gameIds = gameIds.Where(g => matchingGameIds.Contains(g)).ToList();
            if (gameIds.Count == 0)
                return [];
        }

        var heroRows = await db
            .MatchPlayerItems.Where(r => r.AccountId == accountId && gameIds.Contains(r.GameId))
            .ToListAsync(ct);

        var timingRows = await db
            .MatchItemTimings.Where(t => t.AccountId == accountId && gameIds.Contains(t.GameId))
            .ToListAsync(ct);

        var timingGameIds = timingRows.Select(t => t.GameId).Distinct().ToHashSet();
        var itemsByGame = new Dictionary<int, HashSet<int>>();

        foreach (var t in timingRows)
        {
            if (!itemsByGame.TryGetValue(t.GameId, out var set))
                itemsByGame[t.GameId] = set = [];
            set.Add(t.ItemId);
        }

        foreach (var r in heroRows)
        {
            if (!timingGameIds.Contains(r.GameId))
            {
                if (!itemsByGame.TryGetValue(r.GameId, out var set))
                    itemsByGame[r.GameId] = set = [];
                set.Add(r.ItemId);
            }
        }

        return itemsByGame
            .Select(kv => new MatchItemInput
            {
                GameId = kv.Key,
                Won = wonByGame.TryGetValue(kv.Key, out var won) && won,
                Map = mapByGame.TryGetValue(kv.Key, out var m) ? m : string.Empty,
                ItemIds = kv.Value.ToList(),
            })
            .ToList();
    }

    private static bool ShouldFilterByMap(string? map) =>
        !string.IsNullOrWhiteSpace(map)
        && !string.Equals(map, "all", StringComparison.OrdinalIgnoreCase);

    private static HashSet<int> MatchingGameIds(
        IReadOnlyDictionary<int, string> mapByGame,
        string map
    ) =>
        mapByGame
            .Where(kv => string.Equals(kv.Value, map, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToHashSet();
}
