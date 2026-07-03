using HonStats.App.Players;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Persistence;

internal sealed class SqlitePlayerRosterStatsQuery(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IPlayerRosterStatsQuery
{
    public async Task<PlayerRosterStats?> GetAsync(
        Guid accountId,
        string? map = null,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // match_roster carries the per-match metrics but neither Map nor Duration;
        // both live on player_matches. Join on the shared (GameId, AccountId) key
        // (the composite PK of both tables) so each roster row picks up the match's
        // map and duration. INNER JOIN is safe: roster rows are written alongside
        // their player_matches row during indexing, so every roster row matches.
        var rows = await (
            from r in db.MatchRoster
            where r.AccountId == accountId
            join m in db.PlayerMatches
                on new { r.GameId, r.AccountId } equals new { m.GameId, m.AccountId }
            select new RosterMetricRow(
                r.CreepKills,
                r.NeutralKills,
                r.CreepDenies,
                r.BuildingDamage,
                m.Map,
                m.Duration
            )
        ).ToListAsync(ct);

        // Map filter mirrors SqlitePlayerInsightsQuery.GetHeroGamesAsync: the juvio/
        // roster store has no server-side map param, so filter post-fetch. Map values
        // are stored verbatim ("ForestsOfCaldavar" / "MidWars").
        if (ShouldFilterByMap(map))
        {
            rows = rows.Where(r => string.Equals(r.Map, map, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (rows.Count == 0)
            return null;

        // CS = CreepKills + NeutralKills. A game bears on CS only when BOTH halves
        // are present (one null = the row predates the column → exclude, never 0).
        var csGames = rows.Where(r => r.CreepKills.HasValue && r.NeutralKills.HasValue).ToList();
        var avgCreepScore =
            csGames.Count > 0
                ? csGames.Average(r => r.CreepKills!.Value + r.NeutralKills!.Value)
                : (double?)null;

        // CS/min is an aggregate rate (total CS / total minutes), mirroring the
        // GPM/XPM/DPM computation in MapStatsAggregator.PerMinuteRate — not a mean
        // of per-game rates. Zero-duration games are excluded to avoid divide-by-zero.
        var csMinuteGames = csGames.Where(r => r.Duration > 0).ToList();
        var avgCsPerMinute =
            csMinuteGames.Count > 0
                ? csMinuteGames.Sum(r => r.CreepKills!.Value + r.NeutralKills!.Value)
                    / csMinuteGames.Sum(r => r.Duration / 60.0)
                : (double?)null;

        return new PlayerRosterStats(
            AvgCreepScore: avgCreepScore,
            AvgCsPerMinute: avgCsPerMinute,
            AvgDenies: AverageOrNull(rows, r => r.CreepDenies),
            AvgBuildingDamage: AverageOrNull(rows, r => r.BuildingDamage)
        );
    }

    // Mean of a nullable column over the rows where it is present; null when no
    // row carries the value (forward-only columns stay null, never a lying 0).
    private static double? AverageOrNull(
        IReadOnlyList<RosterMetricRow> rows,
        Func<RosterMetricRow, int?> selector
    )
    {
        var bearing = rows.Where(r => selector(r).HasValue).ToList();
        return bearing.Count > 0 ? bearing.Average(r => selector(r)!.Value) : null;
    }

    private static bool ShouldFilterByMap(string? map) =>
        !string.IsNullOrWhiteSpace(map)
        && !string.Equals(map, "all", StringComparison.OrdinalIgnoreCase);

    // Local projection carrying only the columns this query needs from the join.
    private sealed record RosterMetricRow(
        int? CreepKills,
        int? NeutralKills,
        int? CreepDenies,
        int? BuildingDamage,
        string Map,
        int Duration
    );
}
