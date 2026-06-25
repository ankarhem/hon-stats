namespace HonStats.Domain.Players;

public sealed class PlayerSearchResult
{
    public Guid AccountId { get; set; }
    public string Username { get; set; } = string.Empty;
}
