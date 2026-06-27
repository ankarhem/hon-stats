using HonStats.App.Players;
using HonStats.Domain.Players;

namespace HonStats.Infra.Players;

// Local-first IPlayerSearch: resolves username -> accountId from the players
// table (a persistent, write-through cache that survives restarts) before
// hitting juvio. A local hit returns immediately with no API call; a miss
// falls back to juvio and write-throughs the result so subsequent lookups are
// local. Replaces the prior in-memory cache layer.
internal sealed class LocalFirstPlayerSearch(IPlayerSearch juvio, IPlayerNameStore store)
    : IPlayerSearch
{
    public async Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(
        string username,
        CancellationToken ct = default
    )
    {
        var accountId = await store.GetAccountIdByUsernameAsync(username, ct);
        if (accountId is { } id)
            return [new PlayerSearchResult { AccountId = id, Username = username }];

        var juvioResults = await juvio.SearchAsync(username, ct);
        foreach (var r in juvioResults)
            await store.UpsertAsync(r.AccountId, r.Username, null, null, ct);
        return juvioResults;
    }
}
