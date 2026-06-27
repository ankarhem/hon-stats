namespace HonStats.Domain.Players;

public sealed class PlayerCorpusEntry
{
    public Guid AccountId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
}
