namespace HonStats.App.Players;

public interface IPlayerNameStore
{
    Task UpsertAsync(
        Guid accountId,
        string? username,
        string? displayName,
        string? country,
        CancellationToken ct = default
    );

    // Case-insensitive username -> accountId lookup. juvio's getidbyusername is
    // case-insensitive, so the local lookup must be too ("chad" matches "Chad").
    // Returns null when there is no match or username is null/whitespace.
    Task<Guid?> GetAccountIdByUsernameAsync(string username, CancellationToken ct = default);

    // Batch read of identities keyed by accountId. Missing ids are simply absent
    // from the result; an empty input yields an empty dictionary.
    Task<IReadOnlyDictionary<Guid, ResolvedName>> GetByAccountIdsAsync(
        IReadOnlyList<Guid> accountIds,
        CancellationToken ct = default
    );
}
