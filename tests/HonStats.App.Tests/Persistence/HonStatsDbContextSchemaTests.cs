using AwesomeAssertions;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HonStats.App.Tests.Persistence;

public class HonStatsDbContextSchemaTests
{
    [Fact]
    public async Task Migrate_CreatesAllIndexingTables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"honstats-test-{Guid.NewGuid():N}.db");
        try
        {
            await using var db = new HonStatsDbContext(
                new DbContextOptionsBuilder<HonStatsDbContext>()
                    .UseSqlite($"Data Source={path}")
                    .Options
            );
            await db.Database.MigrateAsync();

            var tables = await db
                .Database.SqlQueryRaw<string>(
                    "SELECT name AS Value FROM sqlite_master WHERE type='table' ORDER BY name"
                )
                .ToListAsync();

            tables
                .Should()
                .Contain([
                    "indexed_players",
                    "player_matches",
                    "match_roster",
                    "match_player_items",
                    "match_item_timing",
                    "hero_builds",
                    "teammates",
                    "players",
                ]);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
