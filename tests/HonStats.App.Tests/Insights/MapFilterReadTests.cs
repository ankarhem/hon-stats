using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Domain.Players;
using HonStats.Infra.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class MapFilterReadTests
{
    private static readonly Guid PlayerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PlayerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private const int Hero = 100;

    [Fact]
    public async Task GetHeroItemInputs_FilteredByMap_ReturnsOnlyMatchingGames()
    {
        await using var fixture = await MapFilterFixture.CreateAsync();

        var midwars = await fixture.Raw.GetHeroItemInputsAsync(PlayerA, Hero, "midwars");
        var caldavar = await fixture.Raw.GetHeroItemInputsAsync(PlayerA, Hero, "caldavar");
        var all = await fixture.Raw.GetHeroItemInputsAsync(PlayerA, Hero);

        midwars.Should().ContainSingle();
        midwars[0].GameId.Should().Be(3);
        midwars[0].Map.Should().Be("midwars");
        midwars[0].ItemIds.Should().Equal([40]);

        caldavar.Should().HaveCount(2);
        caldavar.Select(i => i.GameId).Should().Equal([1, 2]);
        caldavar.All(i => i.Map == "caldavar").Should().BeTrue();

        all.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTeammateInputs_FilteredByMap_ReturnsOnlyMatchingGames()
    {
        await using var fixture = await MapFilterFixture.CreateAsync();

        var caldavar = await fixture.Raw.GetTeammateInputsAsync(PlayerA, "caldavar");
        var midwars = await fixture.Raw.GetTeammateInputsAsync(PlayerA, "midwars");

        caldavar.Should().HaveCount(2);
        caldavar.All(t => t.TeammateAccountIds.Contains(PlayerB)).Should().BeTrue();
        caldavar.All(t => !t.TeammateAccountIds.Contains(PlayerC)).Should().BeTrue();

        midwars.Should().ContainSingle();
        midwars[0].TeammateAccountIds.Should().ContainSingle().Which.Should().Be(PlayerC);
    }

    [Fact]
    public async Task GetOverallStats_ReturnsSingleEntryWithMapAll()
    {
        await using var fixture = await MapFilterFixture.CreateAsync();

        var overall = await fixture.Query.GetOverallStatsAsync(PlayerA);

        overall.Map.Should().Be("all");
        overall.Games.Should().Be(3);
        overall.Wins.Should().Be(2);
        overall.WinRate.Should().BeApproximately(2.0 / 3.0, 0.01);
    }

    [Fact]
    public async Task GetHeroBuild_WithMapFilter_ReturnsOnlyThatMapsItems()
    {
        await using var fixture = await MapFilterFixture.CreateAsync();

        var midwarsBuild = await fixture.Query.GetHeroBuildAsync(PlayerA, Hero, "midwars");
        var caldavarBuild = await fixture.Query.GetHeroBuildAsync(PlayerA, Hero, "caldavar");

        midwarsBuild.Should().ContainSingle();
        midwarsBuild[0].ItemId.Should().Be(40);
        midwarsBuild[0].Frequency.Should().Be(1);
        midwarsBuild[0].Games.Should().Be(1);

        caldavarBuild.Select(b => b.ItemId).Should().Equal([10, 20, 30]);
        var item10 = caldavarBuild.Single(b => b.ItemId == 10);
        item10.Frequency.Should().Be(2);
        item10.Games.Should().Be(2);
    }

    [Fact]
    public async Task GetHeroBuild_WithAllMap_BehavesLikeNoFilter()
    {
        await using var fixture = await MapFilterFixture.CreateAsync();

        var withAll = await fixture.Query.GetHeroBuildAsync(PlayerA, Hero, "all");
        var withoutMap = await fixture.Query.GetHeroBuildAsync(PlayerA, Hero);

        withAll.Should().HaveSameCount(withoutMap);
        withAll
            .Should()
            .BeEquivalentTo(
                withoutMap,
                opts => opts.Including(b => b.ItemId).Including(b => b.Frequency)
            );
    }

    [Fact]
    public async Task GetTeammates_WithMapFilter_ReturnsOnlyMatchingMapTeammates()
    {
        await using var fixture = await MapFilterFixture.CreateAsync();

        var midwarsTeammates = await fixture.Query.GetTeammatesAsync(
            PlayerA,
            limit: 25,
            offset: 0,
            "midwars"
        );
        var caldavarTeammates = await fixture.Query.GetTeammatesAsync(
            PlayerA,
            limit: 25,
            offset: 0,
            "caldavar"
        );

        midwarsTeammates
            .Should()
            .ContainSingle(t => t.TeammateAccountId == PlayerC)
            .Which.GamesTogether.Should()
            .Be(1);

        caldavarTeammates
            .Should()
            .ContainSingle(t => t.TeammateAccountId == PlayerB)
            .Which.GamesTogether.Should()
            .Be(2);
        caldavarTeammates.Should().NotContain(t => t.TeammateAccountId == PlayerC);
    }

    private sealed class MapFilterFixture : IAsyncDisposable
    {
        private readonly string _dbPath;
        private readonly ServiceProvider _sp;

        public InsightsRawQuery Raw { get; }
        public SqlitePlayerInsightsQuery Query { get; }

        private MapFilterFixture(
            string dbPath,
            ServiceProvider sp,
            InsightsRawQuery raw,
            SqlitePlayerInsightsQuery query
        )
        {
            _dbPath = dbPath;
            _sp = sp;
            Raw = raw;
            Query = query;
        }

        public static async Task<MapFilterFixture> CreateAsync()
        {
            var dbPath = Path.Combine(
                Path.GetTempPath(),
                $"honstats-mapfilt-{Guid.NewGuid():N}.db"
            );
            var services = new ServiceCollection();
            services.AddDbContextFactory<HonStatsDbContext>(o =>
                o.UseSqlite($"Data Source={dbPath}")
            );
            var sp = services.BuildServiceProvider();
            var factory = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>();

            await using (var db = factory.CreateDbContext())
            {
                await db.Database.MigrateAsync();
                Seed(db);
                await db.SaveChangesAsync();
            }

            var raw = new InsightsRawQuery(factory);
            var query = new SqlitePlayerInsightsQuery(factory, raw);

            return new MapFilterFixture(dbPath, sp, raw, query);
        }

        // Seeds 3 games: 2 on caldavar, 1 on midwars. Different items and teammates per map.
        //
        // Game 1 (caldavar, won):  Hero=100, items [10, 20], teammates [PlayerB]
        // Game 2 (caldavar, lost): Hero=100, items [10, 30], teammates [PlayerB]
        // Game 3 (midwars,  won):  Hero=100, items [40],     teammates [PlayerC]
        private static void Seed(HonStatsDbContext db)
        {
            db.MatchPlayerItems.AddRange(
                Item(1, slot: 0, itemId: 10),
                Item(1, slot: 1, itemId: 20),
                Item(2, slot: 0, itemId: 10),
                Item(2, slot: 1, itemId: 30),
                Item(3, slot: 0, itemId: 40)
            );

            db.MatchRoster.AddRange(
                Roster(
                    1,
                    PlayerA,
                    "Legion",
                    won: true,
                    heroId: Hero,
                    experience: 600,
                    heroDamage: 10_000
                ),
                Roster(1, PlayerB, "Legion", won: true),
                Roster(
                    2,
                    PlayerA,
                    "Legion",
                    won: false,
                    heroId: Hero,
                    experience: 600,
                    heroDamage: 10_000
                ),
                Roster(2, PlayerB, "Legion", won: false),
                Roster(
                    3,
                    PlayerA,
                    "Legion",
                    won: true,
                    heroId: Hero,
                    experience: 300,
                    heroDamage: 5_000
                ),
                Roster(3, PlayerC, "Legion", won: true)
            );

            db.PlayerMatches.AddRange(
                Match(1, "caldavar", kills: 10, deaths: 5, assists: 15, duration: 600, won: true),
                Match(2, "caldavar", kills: 4, deaths: 6, assists: 8, duration: 600, won: false),
                Match(3, "midwars", kills: 20, deaths: 3, assists: 10, duration: 300, won: true)
            );

            db.Players.AddRange(
                new Player
                {
                    AccountId = PlayerB,
                    Username = "bravo",
                    DisplayName = "Bravo",
                    Country = "NO",
                },
                new Player
                {
                    AccountId = PlayerC,
                    Username = "charlie",
                    DisplayName = "Charlie",
                    Country = "SE",
                }
            );

            db.HeroBuilds.AddRange(
                new HeroBuild
                {
                    AccountId = PlayerA,
                    HeroId = Hero,
                    ItemId = 10,
                    Frequency = 2,
                    Wins = 1,
                    Games = 3,
                },
                new HeroBuild
                {
                    AccountId = PlayerA,
                    HeroId = Hero,
                    ItemId = 20,
                    Frequency = 1,
                    Wins = 1,
                    Games = 3,
                },
                new HeroBuild
                {
                    AccountId = PlayerA,
                    HeroId = Hero,
                    ItemId = 30,
                    Frequency = 1,
                    Wins = 0,
                    Games = 3,
                },
                new HeroBuild
                {
                    AccountId = PlayerA,
                    HeroId = Hero,
                    ItemId = 40,
                    Frequency = 1,
                    Wins = 1,
                    Games = 3,
                }
            );

            db.Teammates.AddRange(
                new Teammate
                {
                    AccountId = PlayerA,
                    TeammateAccountId = PlayerB,
                    GamesTogether = 2,
                    WinsTogether = 1,
                },
                new Teammate
                {
                    AccountId = PlayerA,
                    TeammateAccountId = PlayerC,
                    GamesTogether = 1,
                    WinsTogether = 1,
                }
            );
        }

        private static MatchPlayerItem Item(int gameId, int slot, int itemId) =>
            new()
            {
                GameId = gameId,
                AccountId = PlayerA,
                Slot = slot,
                ItemId = itemId,
            };

        private static MatchRoster Roster(
            int gameId,
            Guid accountId,
            string team,
            bool won,
            int? heroId = null,
            int? experience = null,
            int? heroDamage = null
        ) =>
            new()
            {
                GameId = gameId,
                AccountId = accountId,
                Team = team,
                Won = won,
                HeroId = heroId,
                Experience = experience,
                HeroDamage = heroDamage,
            };

        private static PlayerMatch Match(
            int gameId,
            string map,
            int kills,
            int deaths,
            int assists,
            int duration,
            bool won
        ) =>
            new()
            {
                AccountId = PlayerA,
                GameId = gameId,
                HeroId = Hero,
                Team = "Legion",
                WinningTeam = won ? "Legion" : "Hellbourne",
                Kills = kills,
                Deaths = deaths,
                Assists = assists,
                Duration = duration,
                Map = map,
            };

        public async ValueTask DisposeAsync()
        {
            await _sp.DisposeAsync();
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
    }
}
