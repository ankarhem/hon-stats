using HonStats.App.Players;
using HonStats.Domain.Players;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.PlayerSearch;

// Loads the full named-player corpus from the players table (the single source of
// truth for player names). Feeds PlayerNameMatcher. Stateless and singleton —
// depends only on the singleton IDbContextFactory, so injecting it into the
// singleton seed service is safe (no captive-dependency).
internal sealed class SqlitePlayerCorpusQuery(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IPlayerCorpusQuery
{
    public async Task<IReadOnlyList<PlayerCorpusEntry>> GetNamedPlayersAsync(
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var rows = await db
            .Players.Where(p =>
                (p.Username != null && p.Username != "")
                || (p.DisplayName != null && p.DisplayName != "")
            )
            .Select(p => new
            {
                p.AccountId,
                p.Username,
                p.DisplayName,
            })
            .ToListAsync(ct);

        return rows.Select(p => new PlayerCorpusEntry
            {
                AccountId = p.AccountId,
                Username = p.Username,
                DisplayName = p.DisplayName,
            })
            .ToList();
    }
}
