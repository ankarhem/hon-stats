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
    IParsedReplayQuery replays,
    IPlayerRatingsQuery ratings,
    IOptions<IndexingOptions> options,
    ILogger<JuvioPlayerInsightsIndexer> logger
) : IPlayerInsightsIndexer
{
    public async Task IndexAsync(
        Guid accountId,
        bool forceBackfill = false,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var indexed =
            await db.IndexedPlayers.FindAsync(new object?[] { accountId }, ct)
            ?? new IndexedPlayer { AccountId = accountId };
        if (db.Entry(indexed).State == EntityState.Detached)
            db.IndexedPlayers.Add(indexed);

        indexed.Status = IndexingStatus.Indexing;
        await db.SaveChangesAsync(ct);

        try
        {
            await this.DoIndexAsync(db, indexed, accountId, forceBackfill, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Fresh context: the main db may hold a corrupted change tracker
            // (a failed SaveChanges inside DoIndexAsync can leave entries in a
            // Detached state — reusing db here masks the original exception with
            // a secondary "Unexpected entry.EntityState: Detached" throw).
            await using var failDb = await dbFactory.CreateDbContextAsync(CancellationToken.None);
            var fail = await failDb.IndexedPlayers.FindAsync([accountId], CancellationToken.None);
            if (fail is not null)
            {
                fail.Status = IndexingStatus.Failed;
                await failDb.SaveChangesAsync(CancellationToken.None);
            }
            progressTracker.Complete(accountId);
            logger.LogError(ex, "Indexing failed for {AccountId}", accountId);
            throw;
        }
    }

    private async Task DoIndexAsync(
        HonStatsDbContext db,
        IndexedPlayer indexed,
        Guid accountId,
        bool forceBackfill,
        CancellationToken ct
    )
    {
        var recent = new List<PlayerMatch>();
        var pageSize = options.Value.RecentMatchesLimit;
        var lastKnown = forceBackfill ? 0 : indexed.LastIndexedMatchId ?? 0;
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
        var stored = await db
            .PlayerMatches.Where(pm => pm.AccountId == accountId)
            .Select(pm => pm.GameId)
            .ToHashSetAsync(ct);
        var newMatches = recent.Where(r => !stored.Contains(r.GameId)).ToList();

        if (newMatches.Count > 0)
        {
            db.PlayerMatches.AddRange(newMatches);
            await db.SaveChangesAsync(ct);
        }

        var newGameIds = newMatches.Select(m => m.GameId).ToList();
        progressTracker.Start(accountId, newGameIds.Count);

        // Each ingestion phase gets its own DbContext: ExecuteDeleteAsync (untracked
        // bulk ops) must never share a change tracker with tracked Add/SaveChanges.
        // Mixing the two desyncs the identity map — tracked entities survive the
        // ExecuteDelete, AddRange conflicts on the same composite key, and the next
        // SaveChanges throws "Unexpected entry.EntityState: Detached".
        await using (var ingestDb = await dbFactory.CreateDbContextAsync(ct))
        {
            await this.IngestSummariesAsync(ingestDb, accountId, newGameIds, forceBackfill, ct);
        }
        await using (var reprocessDb = await dbFactory.CreateDbContextAsync(ct))
        {
            await this.ReprocessStaleItemTimingsAsync(reprocessDb, accountId, ct);
        }

        // Resolve the player's name to write-through into the players table (the
        // LocalFirstPlayerNameResolver). The result is not assigned to
        // indexed_players — name columns were dropped; players is the single source.
        await names.ResolveAsync(new[] { accountId }, ct);
        if (newGameIds.Count > 0)
            indexed.LastIndexedMatchId = newGameIds.Max();
        indexed.IndexedAt = DateTimeOffset.UtcNow;
        indexed.LastReindexAt = DateTimeOffset.UtcNow;
        indexed.Status = IndexingStatus.Indexed;

        // Capture MMR snapshot (one per UTC day, upsert). Fails softly — a juvio
        // ratings fetch failure must never block the core indexing pipeline.
        try
        {
            var playerRatings = await ratings.GetAsync(accountId, ct);
            if (playerRatings is not null)
            {
                var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date);
                var existing = await db.MmrSnapshots.FindAsync(
                    new object?[] { accountId, today },
                    ct
                );
                if (existing is not null)
                {
                    existing.CurrentMmr = playerRatings.CurrentMmr;
                    existing.RankedCaldavarRating = playerRatings.RankedCaldavarRating;
                    existing.RankedMidwarsRating = playerRatings.RankedMidwarsRating;
                }
                else
                {
                    db.MmrSnapshots.Add(
                        new MmrSnapshot
                        {
                            AccountId = accountId,
                            CapturedDate = today,
                            CurrentMmr = playerRatings.CurrentMmr,
                            RankedCaldavarRating = playerRatings.RankedCaldavarRating,
                            RankedMidwarsRating = playerRatings.RankedMidwarsRating,
                        }
                    );
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MMR snapshot capture failed for {AccountId}", accountId);
        }

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
        bool forceBackfill,
        CancellationToken ct
    )
    {
        if (gameIds.Count == 0)
        {
            if (forceBackfill)
            {
                gameIds = await db
                    .PlayerMatches.Where(m => m.AccountId == accountId)
                    .Select(m => m.GameId)
                    .ToListAsync(ct);
            }
            if (gameIds.Count == 0)
                return;
        }

        List<int> toIngest;
        if (forceBackfill)
        {
            toIngest = gameIds.ToList();
        }
        else
        {
            var alreadyIngested = await db
                .MatchRoster.Where(r => gameIds.Contains(r.GameId))
                .Select(r => r.GameId)
                .Distinct()
                .ToListAsync(ct);
            toIngest = gameIds.Except(alreadyIngested).ToList();

            if (toIngest.Count == 0)
            {
                progressTracker.AddCompleted(accountId, 0);
                return;
            }
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
            if (forceBackfill)
            {
                await db.MatchRoster.Where(r => r.GameId == summary.GameId).ExecuteDeleteAsync(ct);
                await db
                    .MatchPlayerItems.Where(r => r.GameId == summary.GameId)
                    .ExecuteDeleteAsync(ct);
            }

            foreach (var player in summary.Players)
            {
                db.MatchRoster.Add(
                    new MatchRoster
                    {
                        GameId = summary.GameId,
                        AccountId = player.AccountId,
                        Team = player.Team,
                        Won = player.Team == summary.WinningTeam,
                        HeroId = player.HeroId,
                        Kills = player.Kills,
                        Deaths = player.Deaths,
                        Assists = player.Assists,
                        NetWorth = player.NetWorth,
                        Level = player.Level,
                        CreepKills = player.CreepKills,
                        NeutralKills = player.NeutralKills,
                        CreepDenies = player.CreepDenies,
                        BuildingDamage = player.BuildingDamage,
                        Buybacks = player.Buybacks,
                        RavenPlaced = player.RavenPlaced,
                        WardOfSightPlaced = player.WardOfSightPlaced,
                        WardOfRevelationPlaced = player.WardOfRevelationPlaced,
                        RoleIndex = player.RoleIndex,
                        GoldFromCreeps = player.GoldFromCreeps,
                        GoldFromNeutrals = player.GoldFromNeutrals,
                        GoldFromKills = player.GoldFromKills,
                        GoldFromAssists = player.GoldFromAssists,
                        GoldFromBuildings = player.GoldFromBuildings,
                        StartingGold = player.StartingGold,
                        DeathGoldLost = player.DeathGoldLost,
                        Experience = player.Experience > 0 ? (int?)player.Experience : null,
                        HeroDamage = player.HeroDamage > 0 ? player.HeroDamage : null,
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
                            Slot = slot++,
                            ItemId = itemId,
                        }
                    );
                }
            }

            await IngestItemTimingAsync(db, summary.GameId, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    // Optional replay-derived data: per-(game,player,item) first-buy seconds, and
    // end-game hero damage (getmatchsummary's heroDamage is a dead field, so the
    // real total is read from the replay snapshot timeline). Failures are isolated
    // — a missing/unparseable replay must never break the core summary ingestion
    // (already persisted above).
    private async Task IngestItemTimingAsync(HonStatsDbContext db, int gameId, CancellationToken ct)
    {
        try
        {
            var replay = await replays.GetAsync(gameId, ct);
            if (replay is null)
                return;

            if (ItemTimingAggregator.IsDegenerate(replay))
            {
                logger.LogWarning(
                    "Replay for game {GameId} not ready ({SnapshotCount} snapshots), skipping item timing",
                    gameId,
                    replay.Snapshots.Count
                );
                await db.MatchItemTimings.Where(t => t.GameId == gameId).ExecuteDeleteAsync(ct);
                return;
            }

            var heroDamage = HeroDamageAggregator.Build(replay);
            var rosterRows = db.MatchRoster.Local.Where(r => r.GameId == gameId).ToList();
            if (rosterRows.Count == 0)
                rosterRows = await db.MatchRoster.Where(r => r.GameId == gameId).ToListAsync(ct);
            foreach (var r in rosterRows)
                r.HeroDamage = heroDamage.TryGetValue(r.AccountId, out var hd) ? hd : null;

            var rows = ItemTimingAggregator.Build(replay);
            if (rows.Count == 0)
                return;

            await db.MatchItemTimings.Where(t => t.GameId == gameId).ExecuteDeleteAsync(ct);
            db.MatchItemTimings.AddRange(
                rows.Select(r => new MatchItemTiming
                {
                    GameId = r.GameId,
                    AccountId = r.AccountId,
                    ItemId = r.ItemId,
                    FirstSeenSeconds = r.FirstSeenSeconds,
                })
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Replay timing ingest failed for game {GameId}", gameId);
        }
    }

    // Detects games whose item timings carry the degenerate-replay fingerprint
    // (all rows for a game share one FirstSeenSeconds) and re-attempts their replays.
    // If the replay is now ready, good data replaces bad; if still degenerate, the bad
    // rows are cleaned up by IngestItemTimingAsync's ExecuteDeleteAsync.
    private async Task ReprocessStaleItemTimingsAsync(
        HonStatsDbContext db,
        Guid accountId,
        CancellationToken ct
    )
    {
        var staleGames = await db
            .MatchItemTimings.Where(t => t.AccountId == accountId)
            .GroupBy(t => t.GameId)
            .Where(g => g.Min(t => t.FirstSeenSeconds) == g.Max(t => t.FirstSeenSeconds))
            .Select(g => g.Key)
            .ToListAsync(ct);

        if (staleGames.Count == 0)
            return;

        logger.LogInformation(
            "Re-processing {Count} games with stale item timings for {AccountId}",
            staleGames.Count,
            accountId
        );

        foreach (var gameId in staleGames)
            await IngestItemTimingAsync(db, gameId, ct);

        await db.SaveChangesAsync(ct);
    }
}
