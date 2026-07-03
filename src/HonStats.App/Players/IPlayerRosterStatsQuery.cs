namespace HonStats.App.Players;

// Read model: per-game averages over a player's match_roster rows (scoped to the
// selected map — "all" / ForestsOfCaldavar / MidWars). Every field is null when
// the player has no bearing roster rows (not indexed, or no games carry the
// column), so the view degrades to "—" rather than a lying 0.
public sealed record PlayerRosterStats(
    double? AvgCreepScore,
    double? AvgCsPerMinute,
    double? AvgDenies,
    double? AvgBuildingDamage
);

// Local-DB query: averages the per-match-per-player metrics that live on
// match_roster (CS, denies, building damage). map filters via the join to
// player_matches (roster itself carries neither Map nor Duration); pass null or
// "all" for the unfiltered aggregate. Returns null when the player has no roster
// rows (not indexed).
public interface IPlayerRosterStatsQuery
{
    Task<PlayerRosterStats?> GetAsync(
        Guid accountId,
        string? map = null,
        CancellationToken ct = default
    );
}
