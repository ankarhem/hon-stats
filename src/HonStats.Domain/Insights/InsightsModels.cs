namespace HonStats.Domain.Insights;

public enum IndexingStatus
{
    NotIndexed,
    Indexing,
    Indexed,
}

// Persisted + read model (returned directly by IPlayerInsightsQuery — no mapping).
// Configured as an EF entity via IEntityTypeConfiguration<IndexedPlayer> in Infra.
public sealed class IndexedPlayer
{
    public Guid AccountId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Country { get; set; }
    public long? LastIndexedMatchId { get; set; }
    public DateTimeOffset? LastReindexAt { get; set; }
    public DateTimeOffset? IndexedAt { get; set; }
    public IndexingStatus Status { get; set; } = IndexingStatus.NotIndexed;
}

// Persisted raw store: one row per (game, player, slot). Feeds hero-build aggregation.
public sealed class MatchPlayerItem
{
    public int GameId { get; set; }
    public Guid AccountId { get; set; }
    public int HeroId { get; set; }
    public int Slot { get; set; }
    public int ItemId { get; set; }
    public int RoleIndex { get; set; }
    public int WardsPlaced { get; set; }
    public int WardsRevelation { get; set; }
    public int NetWorth { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
}

// Persisted raw store: one row per (game, player). Feeds teammate aggregation
// via a self-join on GameId + Team.
public sealed class MatchRoster
{
    public int GameId { get; set; }
    public Guid AccountId { get; set; }
    public string Team { get; set; } = string.Empty;
    public bool Won { get; set; }
}

// Persisted aggregate: item frequency for a (player, hero).
public sealed class HeroBuild
{
    public Guid AccountId { get; set; }
    public int HeroId { get; set; }
    public int ItemId { get; set; }
    public int Frequency { get; set; }
    public int Games { get; set; }
}

// Persisted aggregate: co-occurrence for a (player, teammate).
public sealed class Teammate
{
    public Guid AccountId { get; set; }
    public Guid TeammateAccountId { get; set; }
    public int GamesTogether { get; set; }
    public int WinsTogether { get; set; }
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    public string? Country { get; set; }
}
