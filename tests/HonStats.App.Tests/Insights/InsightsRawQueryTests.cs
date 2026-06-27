using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Infra.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class InsightsRawQueryTests
{
    private static readonly Guid PlayerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private const int Hero = 100;

    [Fact]
    public async Task GetHeroItemInputs_CarriesWonAndMapPerGame()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-rawq-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddDbContextFactory<HonStatsDbContext>(o =>
                o.UseSqlite($"Data Source={dbPath}")
            );
            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>();

            await using (var db = factory.CreateDbContext())
            {
                await db.Database.MigrateAsync();

                db.MatchPlayerItems.AddRange(
                    Item(gameId: 1, slot: 0, itemId: 10),
                    Item(gameId: 1, slot: 1, itemId: 20),
                    Item(gameId: 2, slot: 0, itemId: 30)
                );
                db.MatchRoster.AddRange(
                    new MatchRoster
                    {
                        GameId = 1,
                        AccountId = PlayerA,
                        Team = "Legion",
                        Won = true,
                    },
                    new MatchRoster
                    {
                        GameId = 2,
                        AccountId = PlayerA,
                        Team = "Legion",
                        Won = false,
                    }
                );
                db.PlayerMatches.AddRange(
                    new PlayerMatch
                    {
                        AccountId = PlayerA,
                        GameId = 1,
                        HeroId = Hero,
                        Map = "caldavar",
                    },
                    new PlayerMatch
                    {
                        AccountId = PlayerA,
                        GameId = 2,
                        HeroId = Hero,
                        Map = "midwars",
                    }
                );
                await db.SaveChangesAsync();
            }

            var raw = new InsightsRawQuery(factory);
            var inputs = await raw.GetHeroItemInputsAsync(PlayerA, Hero);

            inputs
                .Should()
                .ContainSingle(i => i.GameId == 1)
                .Which.Should()
                .BeEquivalentTo(
                    new
                    {
                        GameId = 1L,
                        Won = true,
                        Map = "caldavar",
                        ItemIds = new List<int> { 10, 20 },
                    }
                );
            inputs
                .Should()
                .ContainSingle(i => i.GameId == 2)
                .Which.Should()
                .BeEquivalentTo(
                    new
                    {
                        GameId = 2L,
                        Won = false,
                        Map = "midwars",
                        ItemIds = new List<int> { 30 },
                    }
                );
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static MatchPlayerItem Item(int gameId, int slot, int itemId) =>
        new()
        {
            GameId = gameId,
            AccountId = PlayerA,
            HeroId = Hero,
            Slot = slot,
            ItemId = itemId,
        };
}
