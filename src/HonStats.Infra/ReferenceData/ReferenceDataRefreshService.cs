using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.ReferenceData;

// Loads heroes + items into the cache at startup, then refreshes on a
// configurable interval (default 12h). Failures are logged, not fatal — the
// cache keeps serving the last good snapshot.
internal sealed class ReferenceDataRefreshService(
    CachedReferenceData cache,
    IOptions<ReferenceDataOptions> options,
    ILogger<ReferenceDataRefreshService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await this.RefreshOnce(stoppingToken);

        var intervalHours = Math.Max(1, options.Value.RefreshIntervalHours);
        var interval = TimeSpan.FromHours(intervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await this.RefreshOnce(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshOnce(CancellationToken ct)
    {
        try
        {
            await cache.RefreshAsync(ct);
            var heroes = await cache.GetHeroesAsync(ct);
            var items = await cache.GetItemsAsync(ct);
            var abilities = await cache.GetAbilitiesAsync(ct);
            logger.LogInformation(
                "Reference data refreshed: {Heroes} heroes, {Items} items, {Abilities} abilities",
                heroes.Count,
                items.Count,
                abilities.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reference data refresh failed; will retry next interval");
        }
    }
}
