using HonStats.App.Players;
using HonStats.Domain.Insights;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Persistence;

internal sealed class SqliteMmrHistoryQuery(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IMmrHistoryQuery
{
    public async Task<IReadOnlyList<MmrSnapshot>> GetAsync(
        Guid accountId,
        int days = 30,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var since = DateOnly.FromDateTime(DateTimeOffset.UtcNow.Date.AddDays(-days));
        return await db
            .MmrSnapshots.Where(s => s.AccountId == accountId && s.CapturedDate >= since)
            .OrderBy(s => s.CapturedDate)
            .ToListAsync(ct);
    }
}
