namespace HonStats.Domain.Matches;

// Transient read-shape from getparsedreplay (time-series snapshots, ~every 30s).
// Never persisted here. Player identity is positional (team-index + player-index);
// only snapshot[0]'s players carry SlotIndex + StartingGold as the mapping anchor.
public sealed class ReplayItem
{
    public int ItemId { get; set; }
    public int Slot { get; set; }
}

public sealed class ReplayPlayer
{
    public List<ReplayItem> Items { get; set; } = [];
    public int? NetWorth { get; set; }
    public double? Level { get; set; }
    public double? Experience { get; set; }
    public int? CreepDenies { get; set; }
    public int? RavenPlaced { get; set; }
    public int? SlotIndex { get; set; }
    public int? StartingGold { get; set; }
}

public sealed class ReplayTeam
{
    public List<ReplayPlayer> Players { get; set; } = [];
}

public sealed class ReplaySnapshot
{
    public int Time { get; set; }
    public List<ReplayTeam> Teams { get; set; } = [];
}

public sealed class ParsedReplay
{
    public int GameId { get; set; }
    public string? Date { get; set; }
    public string? WinningTeam { get; set; }
    public List<ReplaySnapshot> Snapshots { get; set; } = [];
}
