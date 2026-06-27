namespace HonStats.Domain.Players;

public sealed class PlayerNameSearchResult
{
    public Guid AccountId { get; set; }
    public string Username { get; set; } = string.Empty; // the searchable name that matched
    public string? DisplayName { get; set; }
    public int Score { get; set; } // 0-100, higher = better; 0 = no match
}
