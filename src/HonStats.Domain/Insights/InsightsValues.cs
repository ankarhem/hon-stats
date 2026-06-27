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
