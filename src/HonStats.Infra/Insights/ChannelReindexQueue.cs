using System.Threading.Channels;
using HonStats.App.Indexing;
using HonStats.Domain.Indexing;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.Insights;

internal sealed record ReindexRequest(Guid AccountId);

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
        CancellationToken ct = default
    )
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

        await channel.Writer.WriteAsync(new ReindexRequest(accountId), ct);
        return new ReindexResult { Outcome = ReindexOutcome.Enqueued };
    }
}
