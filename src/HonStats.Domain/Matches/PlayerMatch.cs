namespace HonStats.Domain.Matches;

// Dual-use: the live juvio DTO from getrecentmatchesforplayer (AccountId set by
// the caller) AND the persisted diff-store row. One model, no mapping.
public sealed class PlayerMatch
{
    public Guid AccountId { get; set; }
    public int GameId { get; set; }
    public int HeroId { get; set; }
    public string Team { get; set; } = string.Empty;
    public string WinningTeam { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public DateTimeOffset Date { get; set; }
    public int Duration { get; set; }
    public string Map { get; set; } = string.Empty;
    public bool IsArranged { get; set; }
}
