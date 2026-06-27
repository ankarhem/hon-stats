namespace HonStats.Domain.Matches;

// Transient read-shape from getmatchsummary. Never persisted (match detail is
// always served live from juvio); the indexed pipeline only extracts roster +
// item rows from it.
public sealed class MatchPlayer
{
    public Guid AccountId { get; set; }
    public string Team { get; set; } = string.Empty;
    public int HeroId { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int CreepKills { get; set; }
    public int NeutralKills { get; set; }
    public int CreepDenies { get; set; }
    public int NetWorth { get; set; }
    public int GoldFromCreeps { get; set; }
    public int GoldFromNeutrals { get; set; }
    public int GoldFromKills { get; set; }
    public int GoldFromAssists { get; set; }
    public int GoldFromBuildings { get; set; }
    public int StartingGold { get; set; }
    public int DeathGoldLost { get; set; }
    public int WardOfSightPlaced { get; set; }
    public int WardOfRevelationPlaced { get; set; }
    public int RavenPlaced { get; set; }
    public double Experience { get; set; }
    public double Level { get; set; }
    public int HeroDamage { get; set; }
    public int BuildingDamage { get; set; }
    public int Buybacks { get; set; }
    public List<int> Inventory { get; set; } = [];
    public int RoleIndex { get; set; }
}

public sealed class MatchDetail
{
    public int GameId { get; set; }
    public DateTimeOffset Date { get; set; }
    public int Duration { get; set; }
    public string WinningTeam { get; set; } = string.Empty;
    public string Map { get; set; } = string.Empty;
    public bool IsArranged { get; set; }
    public List<MatchPlayer> Players { get; set; } = [];
}
