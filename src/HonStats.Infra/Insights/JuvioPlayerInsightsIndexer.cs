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

        try
        {
            await this.DoIndexAsync(db, indexed, accountId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            indexed.Status = IndexingStatus.Failed;
            await db.SaveChangesAsync(CancellationToken.None);
            progressTracker.Complete(accountId);
            logger.LogError(ex, "Indexing failed for {AccountId}", accountId);
            throw;
        }
    }

    private async Task DoIndexAsync(
        HonStatsDbContext db,
        IndexedPlayer indexed,
        Guid accountId,
        CancellationToken ct
    )
    {
        var recent = new List<PlayerMatch>();
        var pageSize = options.Value.RecentMatchesLimit;
        var lastKnown = indexed.LastIndexedMatchId ?? 0;
        for (var offset = 0; ; offset += pageSize)
        {
            var page = await matches.GetRecentForPlayerAsync(accountId, pageSize, offset, ct);
            if (page.Count == 0)
                break;

            var hitKnown = false;
            foreach (var m in page)
            {
                if (m.GameId <= lastKnown)
                {
                    hitKnown = true;
                    break;
                }
                recent.Add(m);
            }
            if (hitKnown || page.Count < pageSize)
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

        // Resolve the player's name to write-through into the players table (the
        // LocalFirstPlayerNameResolver). The result is not assigned to
        // indexed_players — name columns were dropped; players is the single source.
        await names.ResolveAsync(new[] { accountId }, ct);
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

        var alreadyIngested = await db
            .MatchRoster.Where(r => gameIds.Contains(r.GameId))
            .Select(r => r.GameId)
            .Distinct()
            .ToListAsync(ct);
        var toIngest = gameIds.Except(alreadyIngested).ToList();

        if (toIngest.Count == 0)
        {
            progressTracker.AddCompleted(accountId, 0);
            return;
        }

        var concurrency = Math.Max(1, options.Value.MatchSummaryConcurrency);
        using var gate = new SemaphoreSlim(concurrency);
        var summaries = new List<MatchDetail>();
        var summariesLock = new object();

        var fetches = toIngest.Select(async gameId =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var summary = await matches.GetSummaryAsync(gameId, ct);
                if (summary is not null)
                {
                    lock (summariesLock)
                        summaries.Add(summary);
                    progressTracker.AddCompleted(accountId, 1);
                }
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(fetches);

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
                        GoldFromCreeps = player.GoldFromCreeps,
                        GoldFromNeutrals = player.GoldFromNeutrals,
                        GoldFromKills = player.GoldFromKills,
                        GoldFromAssists = player.GoldFromAssists,
                        GoldFromBuildings = player.GoldFromBuildings,
                        StartingGold = player.StartingGold,
                        DeathGoldLost = player.DeathGoldLost,
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
