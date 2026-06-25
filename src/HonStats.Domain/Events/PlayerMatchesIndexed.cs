namespace HonStats.Domain.Events;

// Raised after raw match data for a player has been ingested. App-layer handlers
// rebuild derived aggregates (hero builds, teammates) from the updated raw store.
public sealed class PlayerMatchesIndexed : IDomainEvent
{
    public Guid AccountId { get; init; }
    public List<long> NewGameIds { get; init; } = [];

    public PlayerMatchesIndexed(Guid accountId, IReadOnlyList<long> newGameIds)
    {
        this.AccountId = accountId;
        this.NewGameIds = [.. newGameIds];
    }
}
