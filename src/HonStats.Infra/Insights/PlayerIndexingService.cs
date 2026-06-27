using System.Threading.Channels;
using HonStats.App.Insights;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HonStats.Infra.Insights;

// Consumes the reindex channel and runs ingestion + aggregate rebuild for each
// request in a fresh DI scope. Single reader (the channel is configured for it).
internal sealed class PlayerIndexingService(
    IServiceProvider services,
    Channel<ReindexRequest> channel,
    ILogger<PlayerIndexingService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = services.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<IPlayerInsightsIndexer>();
                await indexer.IndexAsync(request.AccountId, request.ForceBackfill, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Indexing failed for {AccountId}", request.AccountId);
            }
        }
    }
}
