namespace HonStats.Domain.Players;

// Single source of truth for player identity (username, display name, country).
// Write-through populated by both name-resolution ports (IPlayerNameResolver and
// IPlayerSearch) via their persisting decorators. Read by the profile local-first
// lookup, the teammate JOIN, and the search corpus query.
public sealed class Player
{
    public Guid AccountId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Country { get; set; }
}
