using System.Threading.Channels;
using HonStats.App.Indexing;
using HonStats.Domain.Indexing;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.Insights;

internal sealed record ReindexRequest(Guid AccountId, bool ForceBackfill = false);

// Cooldown-gated queue: rejects a reindex request for a player whose last index
// completed within ReindexCooldownMinutes, otherwise enqueues it for the
// background PlayerIndexingService.
internal sealed class ChannelReindexQueue(
    Channel<ReindexRequest> channel,
    IDbContextFactory<HonStatsDbContext> dbFactory,
    IOptions<IndexingOptions> options
) : IReindexQueue
{
    public async Task<ReindexResult> RequestReindexAsync(
        Guid accountId,
        bool forceBackfill = false,
        CancellationToken ct = default
    )
    {
        if (!forceBackfill)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var indexed = await db.IndexedPlayers.FindAsync(new object?[] { accountId }, ct);

            var cooldown = TimeSpan.FromMinutes(Math.Max(1, options.Value.ReindexCooldownMinutes));
            if (
                indexed?.LastReindexAt is DateTimeOffset last
                && DateTimeOffset.UtcNow - last < cooldown
            )
            {
                return new ReindexResult
                {
                    Outcome = ReindexOutcome.CooldownRejected,
                    RetryAfter = cooldown - (DateTimeOffset.UtcNow - last),
                };
            }
        }

        await channel.Writer.WriteAsync(new ReindexRequest(accountId, forceBackfill), ct);
        return new ReindexResult { Outcome = ReindexOutcome.Enqueued };
    }
}
