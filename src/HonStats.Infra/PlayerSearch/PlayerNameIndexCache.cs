using HonStats.App.Players;
using HonStats.App.Search;
using HonStats.Domain.Players;
using Microsoft.Extensions.Logging;

namespace HonStats.Infra.PlayerSearch;

// Singleton in-memory cache holding the current immutable PlayerNameMatcher snapshot.
// Mirrors CachedReferenceData: a SemaphoreSlim gate serializes writers, while readers
// take a lock-free volatile read of the matcher field and operate on that immutable
// instance. The field write at the end of a successful build is the volatile write that
// publishes the new matcher to all readers.
//
// _stale starts true (no matcher yet). PlayerNameIndexRefreshHandler flips it on a
// PlayerMatchesIndexed event; the seed service's poll loop notices within one
// PollInterval and rebuilds. On a failed build _stale is set back true and the
// exception propagates so the caller (seed service) can log it and retry — meanwhile
// the last good matcher keeps serving searches.
internal sealed class PlayerNameIndexCache
{
    private volatile PlayerNameMatcher? _matcher;
    private volatile bool _stale = true;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<PlayerNameIndexCache> _logger;

    public PlayerNameIndexCache(ILogger<PlayerNameIndexCache> logger)
    {
        _logger = logger;
    }

    public int Count => _matcher?.Count ?? 0;

    public bool IsStale => _stale || _matcher is null;

    // Lock-free read path: snapshot the volatile matcher reference, then use that
    // immutable instance. A null matcher (pre-first-build) yields an empty result.
    public IReadOnlyList<PlayerNameSearchResult> Search(string query, int limit)
    {
        var local = _matcher;
        return local is null ? Array.Empty<PlayerNameSearchResult>() : local.Search(query, limit);
    }

    public void MarkStale()
    {
        _stale = true;
    }

    public async Task RebuildAsync(IPlayerCorpusQuery query, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var corpus = await query.GetNamedPlayersAsync(ct);
            var matcher = new PlayerNameMatcher(corpus);
            _matcher = matcher;
            _stale = false;
            _logger.LogInformation("Rebuilt player name index: {Count} entries", matcher.Count);
        }
        catch
        {
            // Publish no new matcher; keep the last good one serving. Re-flag stale so
            // the poll loop retries on the next interval, then rethrow for logging.
            _stale = true;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }
}
