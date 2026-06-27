using HonStats.App.Players;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HonStats.Infra.Players;

internal sealed class SqlitePlayerNameStore(IDbContextFactory<HonStatsDbContext> dbFactory)
    : IPlayerNameStore
{
    public async Task<Guid?> GetAccountIdByUsernameAsync(
        string username,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // lower() on both sides for a case-insensitive match mirroring juvio's
        // getidbyusername. NULL usernames yield lower(NULL) = NULL, which never
        // compares equal, so they are naturally excluded.
        var needle = username.ToLowerInvariant();
        var player = await db.Players.FirstOrDefaultAsync(
            p => p.Username != null && p.Username.ToLower() == needle,
            ct
        );
        return player?.AccountId;
    }

    public async Task<IReadOnlyDictionary<Guid, ResolvedName>> GetByAccountIdsAsync(
        IReadOnlyList<Guid> accountIds,
        CancellationToken ct = default
    )
    {
        if (accountIds.Count == 0)
            return new Dictionary<Guid, ResolvedName>();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var players = await db.Players.Where(p => accountIds.Contains(p.AccountId)).ToListAsync(ct);

        return players.ToDictionary(
            p => p.AccountId,
            p => new ResolvedName
            {
                AccountId = p.AccountId,
                DisplayName = p.DisplayName,
                Username = p.Username,
                Country = p.Country,
            }
        );
    }

    public async Task UpsertAsync(
        Guid accountId,
        string? username,
        string? displayName,
        string? country,
        CancellationToken ct = default
    )
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (username is not null)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE players SET Username = NULL WHERE Username = {username} AND AccountId <> {accountId};",
                ct
            );
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO players (AccountId, Username, DisplayName, Country)
            VALUES ({accountId}, {username}, {displayName}, {country})
            ON CONFLICT(AccountId) DO UPDATE SET
              Username = COALESCE({username}, Username),
              DisplayName = COALESCE({displayName}, DisplayName),
              Country = COALESCE({country}, Country);
            """,
            ct
        );
    }
}
