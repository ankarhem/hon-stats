using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Insights;

internal sealed class InsightsAggregateStore(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IInsightsAggregateStore
{
    public async Task ReplaceHeroBuildsAsync(
        Guid accountId,
        int heroId,
        IReadOnlyList<HeroBuildEntry> entries,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = db.HeroBuilds.Where(h => h.AccountId == accountId && h.HeroId == heroId);
        db.HeroBuilds.RemoveRange(existing);

        foreach (var entry in entries)
        {
            db.HeroBuilds.Add(
                new HeroBuild
                {
                    AccountId = accountId,
                    HeroId = heroId,
                    ItemId = entry.ItemId,
                    Frequency = entry.Frequency,
                    Wins = entry.Wins,
                    Games = entry.Games,
                }
            );
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ReplaceHeroItemPairsAsync(
        Guid accountId,
        int heroId,
        IReadOnlyList<HeroItemPairEntry> entries,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = db.HeroItemPairs.Where(h => h.AccountId == accountId && h.HeroId == heroId);
        db.HeroItemPairs.RemoveRange(existing);

        foreach (var entry in entries)
        {
            db.HeroItemPairs.Add(
                new HeroItemPair
                {
                    AccountId = accountId,
                    HeroId = heroId,
                    ItemA = entry.ItemA,
                    ItemB = entry.ItemB,
                    Frequency = entry.Frequency,
                    Games = entry.Games,
                }
            );
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task ReplaceTeammatesAsync(
        Guid accountId,
        IReadOnlyList<Teammate> teammates,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = db.Teammates.Where(t => t.AccountId == accountId);
        db.Teammates.RemoveRange(existing);

        foreach (var teammate in teammates)
        {
            db.Teammates.Add(teammate);
        }

        await db.SaveChangesAsync(ct);
    }
}
