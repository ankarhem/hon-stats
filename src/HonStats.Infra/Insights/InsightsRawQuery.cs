using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Insights;

internal sealed class InsightsRawQuery(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IInsightsRawQuery
{
    public async Task<IReadOnlyList<int>> GetHeroIdsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db
            .MatchPlayerItems.Where(i => i.AccountId == accountId)
            .Select(i => i.HeroId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MatchItemInput>> GetHeroItemInputsAsync(
        Guid accountId,
        int heroId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db
            .MatchPlayerItems.Where(i => i.AccountId == accountId && i.HeroId == heroId)
            .ToListAsync(ct);

        return rows.GroupBy(r => r.GameId)
            .Select(g => new MatchItemInput
            {
                GameId = g.Key,
                ItemIds = g.Select(x => x.ItemId).ToList(),
            })
            .ToList();
    }

    public async Task<IReadOnlyList<MatchTeammateInput>> GetTeammateInputsAsync(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var subjectGames = await db
            .MatchRoster.Where(r => r.AccountId == accountId)
            .ToListAsync(ct);
        if (subjectGames.Count == 0)
            return [];

        var gameIds = subjectGames.Select(g => g.GameId).Distinct().ToList();
        var allForGames = await db
            .MatchRoster.Where(r => gameIds.Contains(r.GameId))
            .ToListAsync(ct);

        return subjectGames
            .Select(g => new MatchTeammateInput
            {
                GameId = g.GameId,
                Won = g.Won,
                TeammateAccountIds = allForGames
                    .Where(r =>
                        r.GameId == g.GameId && r.Team == g.Team && r.AccountId != accountId
                    )
                    .Select(r => r.AccountId)
                    .ToList(),
            })
            .ToList();
    }
}
