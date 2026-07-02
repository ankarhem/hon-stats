using HonStats.App.Indexing;
using HonStats.Domain.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.Insights;

// Periodically reindexes all players whose status is Indexed, so MMR snapshots
// (and match history) stay fresh. Runs on a configurable interval (default 6h).
// Uses IServiceScopeFactory because IReindexQueue is scoped (ChannelReindexQueue
// reads IndexedPlayer from a scoped DbContext) — singletons cannot consume scoped.
internal sealed class ScheduledReindexService(
    IServiceScopeFactory scopeFactory,
    IOptions<IndexingOptions> options,
    ILogger<ScheduledReindexService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.ReindexIntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var dbFactory = scope.ServiceProvider.GetRequiredService<
                    IDbContextFactory<HonStatsDbContext>
                >();
                var queue = scope.ServiceProvider.GetRequiredService<IReindexQueue>();

                await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
                var playerIds = await db
                    .IndexedPlayers.Where(p => p.Status == IndexingStatus.Indexed)
                    .Select(p => p.AccountId)
                    .ToListAsync(stoppingToken);

                logger.LogInformation(
                    "Scheduled reindex: enqueuing {Count} players",
                    playerIds.Count
                );

                foreach (var accountId in playerIds)
                {
                    await queue.RequestReindexAsync(accountId, forceBackfill: false, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled reindex cycle failed");
            }
        }
    }
}
