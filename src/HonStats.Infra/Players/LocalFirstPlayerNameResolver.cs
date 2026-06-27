using HonStats.App.Players;

namespace HonStats.Infra.Players;

// Local-first IPlayerNameResolver: batch-reads known accountIds from the
// players table (a persistent, write-through cache that survives restarts) and
// only asks juvio for the misses, write-through'ing those so future
// resolutions are local. Replaces the prior in-memory cache layer. The
// match-detail modal resolves 10 names per open; once players is warm that is
// 0 juvio calls.
internal sealed class LocalFirstPlayerNameResolver(
    IPlayerNameResolver juvio,
    IPlayerNameStore store
) : IPlayerNameResolver
{
    public async Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default
    )
    {
        var local = await store.GetByAccountIdsAsync(accountIds.ToList(), ct);
        var misses = accountIds.Where(id => !local.ContainsKey(id)).ToList();
        if (misses.Count == 0)
            return local;

        var juvioResolved = await juvio.ResolveAsync(misses, ct);
        foreach (var (id, name) in juvioResolved)
            await store.UpsertAsync(id, name.Username, name.DisplayName, name.Country, ct);

        var merged = new Dictionary<Guid, ResolvedName>(local);
        foreach (var kv in juvioResolved)
            merged[kv.Key] = kv.Value;
        return merged;
    }
}
