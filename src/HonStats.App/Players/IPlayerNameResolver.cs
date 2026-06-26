namespace HonStats.App.Players;

public sealed class ResolvedName
{
    public Guid AccountId { get; init; }
    public string? DisplayName { get; init; }
    public string? Username { get; init; }
    public string? Country { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

public interface IPlayerNameResolver
{
    Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default
    );
}
