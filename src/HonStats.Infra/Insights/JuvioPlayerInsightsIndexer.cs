using HonStats.App.Events;
using HonStats.App.Insights;
using HonStats.App.Matches;
using HonStats.App.Players;
using HonStats.Domain.Events;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.Insights;

// Ingests a player's match history into the raw store (diff-based: only games we
// have not already stored), then raises PlayerMatchesIndexed so rebuild handlers
// refresh the derived aggregates.
internal sealed class JuvioPlayerInsightsIndexer(
    IDbContextFactory<HonStatsDbContext> dbFactory,
    IMatchQuery matches,
    IPlayerNameResolver names,
    IDomainEventDispatcher dispatcher,
    IIndexProgressTracker progressTracker,
    IOptions<IndexingOptions> options,
    ILogger<JuvioPlayerInsightsIndexer> logger
) : IPlayerInsightsIndexer
{
    public async Task IndexAsync(Guid accountId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var indexed =
            await db.IndexedPlayers.FindAsync(new object?[] { accountId }, ct)
            ?? new IndexedPlayer { AccountId = accountId };
        if (indexed.AccountId == accountId && db.Entry(indexed).State == EntityState.Detached)
            db.IndexedPlayers.Add(indexed);

        indexed.Status = IndexingStatus.Indexing;
        await db.SaveChangesAsync(ct);

        var recent = new List<PlayerMatch>();
        var pageSize = options.Value.RecentMatchesLimit;
        for (var offset = 0; ; offset += pageSize)
        {
            var page = await matches.GetRecentForPlayerAsync(accountId, pageSize, offset, ct);
            if (page.Count == 0)
                break;
            recent.AddRange(page);
            if (page.Count < pageSize)
                break;
        }
        var stored = (
            await db
                .PlayerMatches.Where(pm => pm.AccountId == accountId)
                .Select(pm => pm.GameId)
                .ToListAsync(ct)
        ).ToHashSet();
        var newMatches = recent.Where(r => !stored.Contains(r.GameId)).ToList();

        if (newMatches.Count > 0)
        {
            db.PlayerMatches.AddRange(newMatches);
            await db.SaveChangesAsync(ct);
        }

        var newGameIds = newMatches.Select(m => m.GameId).ToList();
        progressTracker.Start(accountId, newGameIds.Count);
        await this.IngestSummariesAsync(db, accountId, newGameIds, ct);

        var resolved = await names.ResolveAsync(new[] { accountId }, ct);
        if (resolved.TryGetValue(accountId, out var name))
        {
            indexed.Username = name.Username;
            indexed.DisplayName = name.DisplayName;
            indexed.Country = name.Country;
        }
        if (newGameIds.Count > 0)
            indexed.LastIndexedMatchId = newGameIds.Max();
        indexed.IndexedAt = DateTimeOffset.UtcNow;
        indexed.LastReindexAt = DateTimeOffset.UtcNow;
        indexed.Status = IndexingStatus.Indexed;
        await db.SaveChangesAsync(ct);

        await dispatcher.DispatchAsync(
            new PlayerMatchesIndexed(accountId, newGameIds.Select(g => (long)g).ToList()),
            ct
        );
        progressTracker.Complete(accountId);
        logger.LogInformation(
            "Indexed {AccountId}: {Count} new matches",
            accountId,
            newGameIds.Count
        );
    }

    private async Task IngestSummariesAsync(
        HonStatsDbContext db,
        Guid accountId,
        IReadOnlyList<int> gameIds,
        CancellationToken ct
    )
    {
        if (gameIds.Count == 0)
            return;

        var concurrency = Math.Max(1, options.Value.MatchSummaryConcurrency);
        using var gate = new SemaphoreSlim(concurrency);
        var summaries = new List<MatchDetail>();
        var summariesLock = new object();

        var fetches = gameIds.Select(async gameId =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var summary = await matches.GetSummaryAsync(gameId, ct);
                if (summary is not null)
                    lock (summariesLock)
                        summaries.Add(summary);
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(fetches);
        progressTracker.AddCompleted(accountId, summaries.Count);

        foreach (var summary in summaries)
        {
            foreach (var player in summary.Players)
            {
                db.MatchRoster.Add(
                    new MatchRoster
                    {
                        GameId = summary.GameId,
                        AccountId = player.AccountId,
                        Team = player.Team,
                        Won = player.Team == summary.WinningTeam,
                    }
                );
                var slot = 0;
                foreach (var itemId in player.Inventory)
                {
                    db.MatchPlayerItems.Add(
                        new MatchPlayerItem
                        {
                            GameId = summary.GameId,
                            AccountId = player.AccountId,
                            HeroId = player.HeroId,
                            Slot = slot++,
                            ItemId = itemId,
                            RoleIndex = player.RoleIndex,
                            WardsPlaced = player.WardOfSightPlaced,
                            WardsRevelation = player.WardOfRevelationPlaced,
                            NetWorth = player.NetWorth,
                            Kills = player.Kills,
                            Deaths = player.Deaths,
                            Assists = player.Assists,
                        }
                    );
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
