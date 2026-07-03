namespace HonStats.Domain.Insights;

public enum IndexingStatus
{
    NotIndexed,
    Indexing,
    Indexed,
    Failed,
}

// Persisted + read model (returned directly by IPlayerInsightsQuery — no mapping).
// Configured as an EF entity via IEntityTypeConfiguration<IndexedPlayer> in Infra.
public sealed class IndexedPlayer
{
    public Guid AccountId { get; set; }
    public long? LastIndexedMatchId { get; set; }
    public DateTimeOffset? LastReindexAt { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
    public IndexingStatus Status { get; set; } = IndexingStatus.NotIndexed;
}

// Persisted raw store: one row per (game, player, slot). Pure item-slot store —
// feeds hero-build aggregation. All per-match-per-player scalars live on
// MatchRoster (the single 1:1 summary row); this table carries only which item
// occupied which slot.
public sealed class MatchPlayerItem
{
    public int GameId { get; set; }
    public Guid AccountId { get; set; }
    public int Slot { get; set; }
    public int ItemId { get; set; }
}

// Persisted raw store: one row per (game, player). The single clean 1:1
// per-match-per-player summary row. Feeds teammate aggregation via a self-join
// on GameId + Team, and carries all per-match metrics (KDA, wards, net worth,
// gold breakdown, experience, hero/building damage).
public sealed class MatchRoster
{
    public int GameId { get; set; }
    public Guid AccountId { get; set; }
    public string Team { get; set; } = string.Empty;
    public bool Won { get; set; }

    // HeroId is nullable because historical 0-item games (where the player had
    // no inventory rows to backfill from) have no source for it — the migration
    // lifts HeroId from match_player_items, and those games leave it null.
    // New indexing always writes HeroId.
    public int? HeroId { get; set; }

    // Per-match metrics. Nullable/forward-only: rows ingested before these
    // columns existed stay null (never a lying 0).
    public int? Kills { get; set; }
    public int? Deaths { get; set; }
    public int? Assists { get; set; }
    public int? NetWorth { get; set; }
    public double? Level { get; set; }
    public int? CreepKills { get; set; }
    public int? NeutralKills { get; set; }
    public int? CreepDenies { get; set; }
    public int? BuildingDamage { get; set; }
    public int? Buybacks { get; set; }
    public int? RavenPlaced { get; set; }
    public int? WardOfSightPlaced { get; set; }
    public int? WardOfRevelationPlaced { get; set; }
    public int? RoleIndex { get; set; }

    // Per-game gold breakdown (feeds per-map GPM). Nullable/forward-only: rows
    // ingested before these columns existed stay null.
    public int? DeathGoldLost { get; set; }
    public int? Experience { get; set; }
    public int? GoldFromAssists { get; set; }
    public int? GoldFromBuildings { get; set; }
    public int? GoldFromCreeps { get; set; }
    public int? GoldFromKills { get; set; }
    public int? GoldFromNeutrals { get; set; }
    public int? HeroDamage { get; set; }
    public int? StartingGold { get; set; }
}

// Persisted aggregate: item frequency + wins for a (player, hero).
public sealed class HeroBuild
{
    public Guid AccountId { get; set; }
    public int HeroId { get; set; }
    public int ItemId { get; set; }
    public int Frequency { get; set; }
    public int Wins { get; set; }
    public int Games { get; set; }
}

// Persisted aggregate: co-occurrence for a (player, teammate).
public sealed class Teammate
{
    public Guid AccountId { get; set; }
    public Guid TeammateAccountId { get; set; }
    public int GamesTogether { get; set; }
    public int WinsTogether { get; set; }
}

// Persisted raw store: first in-game second an item was seen for a (game, player).
// Sourced from replay snapshots (see ItemTimingAggregator). HeroId is intentionally
// absent — the read joins match_player_items on (AccountId, GameId, ItemId) to scope
// a player's games on a hero.
public sealed class MatchItemTiming
{
    public int GameId { get; set; }
    public Guid AccountId { get; set; }
    public int ItemId { get; set; }
    public int FirstSeenSeconds { get; set; }
}

// Persisted MMR snapshot: one row per (player, UTC date). Captured during reindex
// via getprofilestats. Upsert semantics — if reindexed again same day, the row is updated.
public sealed class MmrSnapshot
{
    public Guid AccountId { get; set; }
    public DateOnly CapturedDate { get; set; }
    public double CurrentMmr { get; set; }
    public double RankedCaldavarRating { get; set; }
    public double RankedMidwarsRating { get; set; }
}
