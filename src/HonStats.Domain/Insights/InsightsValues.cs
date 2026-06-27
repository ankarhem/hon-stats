namespace HonStats.Domain.Insights;

// Read model: a single item's pick frequency + win count for a player+hero.
public sealed class HeroBuildEntry
{
    public int ItemId { get; set; }
    public int Frequency { get; set; }
    public int Wins { get; set; }
    public int Games { get; set; }
}

// Read model: how often two items co-occur (bought together) across a player+hero's
// matches. Canonicalized so ItemA < ItemB (no ordered/ self pairs).
public sealed class HeroItemPairEntry
{
    public int ItemA { get; set; }
    public int ItemB { get; set; }
    public int Frequency { get; set; }
    public int Games { get; set; }
}

// Read model: a teammate row enriched with display info (resolved via getuserinfo).
public sealed class TeammateStat
{
    public Guid TeammateAccountId { get; set; }
    public string? DisplayName { get; set; }
    public string? Username { get; set; }
    public string? Country { get; set; }
    public int GamesTogether { get; set; }
    public int WinsTogether { get; set; }
}

// Value objects the pure aggregators consume/produce. No persistence.
public sealed class MatchItemInput
{
    public long GameId { get; set; }
    public bool Won { get; set; }
    public string Map { get; set; } = string.Empty;
    public List<int> ItemIds { get; set; } = [];
}

public sealed class MatchTeammateInput
{
    public long GameId { get; set; }
    public bool Won { get; set; }
    public List<Guid> TeammateAccountIds { get; set; } = [];
}

public sealed class TeammateAggregate
{
    public Guid TeammateAccountId { get; set; }
    public int GamesTogether { get; set; }
    public int WinsTogether { get; set; }
}

// Value object consumed by MapStatsAggregator: one row per played match. GoldEarned
// is null when the gold breakdown is absent (pre-gold-ingestion matches).
// WardsPlaced is the subject's observer-ward count for that game (sourced from
// match_player_items; 0 when the player has no item rows for the game).
public sealed class MatchStatInput
{
    public long GameId { get; set; }
    public string Map { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int? GoldEarned { get; set; }
    public int DurationSeconds { get; set; }
    public bool Won { get; set; }
    public int WardsPlaced { get; set; }
}

// Read model produced by MapStatsAggregator: per-map KDA + GPM + win rate.
// AvgGPM is null when no games for the map carry a gold breakdown.
public sealed class MapStatEntry
{
    public string Map { get; set; } = string.Empty;
    public int Games { get; set; }
    public double AvgKills { get; set; }
    public double AvgDeaths { get; set; }
    public double AvgAssists { get; set; }
    public double AvgWards { get; set; }
    public double? AvgGPM { get; set; }
    public int Wins { get; set; }
    public double WinRate { get; set; }
}

// Value object produced by ItemTimingAggregator: first in-game second an item was
// seen for a (game, player). Maps 1:1 onto a MatchItemTiming row.
public sealed class ItemBuyTime
{
    public int GameId { get; set; }
    public Guid AccountId { get; set; }
    public int ItemId { get; set; }
    public int FirstSeenSeconds { get; set; }
}

// Read model: mean first-buy second + game count for one item on a player+hero.
public sealed class ItemTimingEntry
{
    public int ItemId { get; set; }
    public double AvgSeconds { get; set; }
    public int Games { get; set; }
}
