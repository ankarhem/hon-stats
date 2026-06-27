using HonStats.App.Players;
using HonStats.Domain.Players;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.PlayerSearch;

// Loads the full named-player corpus from SQLite: every teammate with a usable name
// (the teammate's own account id + resolved name) UNION ALL every indexed player
// with a usable name. The corpus feeds PlayerNameMatcher. Stateless and singleton —
// depends only on the singleton IDbContextFactory, so injecting it into the singleton
// seed service is safe (no captive-dependency).
internal sealed class SqlitePlayerCorpusQuery(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IPlayerCorpusQuery
{
    public async Task<IReadOnlyList<PlayerCorpusEntry>> GetNamedPlayersAsync(
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Project anonymously on the server (UNION ALL via Concat) — do NOT project
        // directly into the Domain PlayerCorpusEntry in the SQL-translated query.
        // Teammate.TeammateAccountId is the player's own id; Teammate.AccountId is the
        // profile the teammate row belongs to, which is NOT the name we want to index.
        var rows = await db
            .Teammates.Where(t =>
                (t.Username != null && t.Username != "")
                || (t.DisplayName != null && t.DisplayName != "")
            )
            .Select(t => new
            {
                AccountId = t.TeammateAccountId,
                t.Username,
                t.DisplayName,
            })
            .Concat(
                db.IndexedPlayers.Where(p =>
                        (p.Username != null && p.Username != "")
                        || (p.DisplayName != null && p.DisplayName != "")
                    )
                    .Select(p => new
                    {
                        p.AccountId,
                        p.Username,
                        p.DisplayName,
                    })
            )
            .ToListAsync(ct);

        return rows.DistinctBy(x => x.AccountId)
            .Select(x => new PlayerCorpusEntry
            {
                AccountId = x.AccountId,
                Username = x.Username,
                DisplayName = x.DisplayName,
            })
            .ToList();
    }
}
