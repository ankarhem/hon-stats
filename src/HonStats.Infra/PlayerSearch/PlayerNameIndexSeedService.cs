using HonStats.App.Players;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.PlayerSearch;

// Seeds the player name index at startup, then keeps it fresh by rebuilding whenever
// the cache is stale. PlayerNameIndexRefreshHandler flips the stale flag on a
// PlayerMatchesIndexed event; this loop notices within one PollInterval and rebuilds,
// bounding the rebuild rate to one per PollInterval during indexing bursts.
//
// Mirrors ReferenceDataRefreshService's resilience: rebuild failures are logged, not
// fatal — the cache keeps serving the last good matcher, and the next poll retries.
internal sealed class PlayerNameIndexSeedService(
    PlayerNameIndexCache cache,
    IPlayerCorpusQuery query,
    IOptions<PlayerNameSearchOptions> options,
    ILogger<PlayerNameIndexSeedService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial build before the loop: do NOT rethrow — the app must start even if
        // the DB is briefly unavailable. The loop below will retry on the next poll.
        try
        {
            await cache.RebuildAsync(query, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Initial player name index build failed; will retry on poll");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.Value.PollInterval, stoppingToken);
                if (cache.IsStale)
                {
                    await cache.RebuildAsync(query, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // One failure must not kill the service; keep serving the last snapshot.
                logger.LogError(ex, "Player name index rebuild failed; will retry next poll");
            }
        }
    }
}
