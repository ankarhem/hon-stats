using AwesomeAssertions;
using HonStats.App.Events;
using HonStats.App.Insights;
using HonStats.App.Matches;
using HonStats.App.Players;
using HonStats.Domain.Events;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Infra.Insights;
using HonStats.Infra.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class IndexingPipelineTests
{
    private static readonly Guid PlayerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PlayerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PlayerC = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PlayerD = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private const int Hero = 100;

    [Fact]
    public async Task Index_IngestsMatches_AndRebuildsHeroBuildsAndTeammates()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-idx-{Guid.NewGuid():N}.db");
        try
        {
            var matchQuery = CreateMatchQuery();
            var nameResolver = new FakeNameResolver(
                new Dictionary<Guid, ResolvedName>
                {
                    [PlayerA] = new()
                    {
                        AccountId = PlayerA,
                        Username = "alpha",
                        Country = "SE",
                    },
                    [PlayerB] = new()
                    {
                        AccountId = PlayerB,
                        Username = "bravo",
                        Country = "NO",
                    },
                }
            );

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<HonStatsDbContext>(o =>
                o.UseSqlite($"Data Source={dbPath}")
            );
            services.Configure<IndexingOptions>(o =>
            {
                o.RecentMatchesLimit = 50;
                o.MatchSummaryConcurrency = 2;
            });
            services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
            services.AddSingleton<IIndexProgressTracker, IndexProgressTracker>();
            services.AddScoped<IInsightsRawQuery, InsightsRawQuery>();
            services.AddScoped<IInsightsAggregateStore, InsightsAggregateStore>();
            services.AddScoped<IPlayerInsightsQuery, SqlitePlayerInsightsQuery>();
            services.AddScoped<IPlayerInsightsIndexer, JuvioPlayerInsightsIndexer>();
            services.AddScoped<IEventHandler<PlayerMatchesIndexed>, RebuildHeroBuildsHandler>();
            services.AddScoped<IEventHandler<PlayerMatchesIndexed>, RebuildTeammatesHandler>();
            services.AddSingleton<IMatchQuery>(matchQuery);
            services.AddSingleton<IPlayerNameResolver>(nameResolver);
            var sp = services.BuildServiceProvider();

            await using (
                var db = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>()
                    .CreateDbContext()
            )
            {
                await db.Database.MigrateAsync();
            }

            using var scope = sp.CreateScope();
            var indexer = scope.ServiceProvider.GetRequiredService<IPlayerInsightsIndexer>();
            await indexer.IndexAsync(PlayerA);

            var query = scope.ServiceProvider.GetRequiredService<IPlayerInsightsQuery>();

            var indexed = await query.GetIndexedPlayerAsync(PlayerA);
            indexed.Should().NotBeNull();
            indexed!.Status.Should().Be(IndexingStatus.Indexed);
            indexed.Username.Should().Be("alpha");

            var build = await query.GetHeroBuildAsync(PlayerA, Hero);
            build
                .Should()
                .ContainEquivalentOf(
                    new HeroBuildEntry
                    {
                        ItemId = 10,
                        Frequency = 2,
                        Games = 2,
                    }
                );
            build
                .Should()
                .ContainEquivalentOf(
                    new HeroBuildEntry
                    {
                        ItemId = 20,
                        Frequency = 1,
                        Games = 2,
                    }
                );
            build
                .Should()
                .ContainEquivalentOf(
                    new HeroBuildEntry
                    {
                        ItemId = 30,
                        Frequency = 1,
                        Games = 2,
                    }
                );

            var teammates = await query.GetTeammatesAsync(PlayerA, limit: 25, offset: 0);
            teammates
                .Should()
                .ContainSingle(t => t.TeammateAccountId == PlayerB)
                .Which.Should()
                .BeEquivalentTo(
                    new
                    {
                        TeammateAccountId = PlayerB,
                        GamesTogether = 2,
                        WinsTogether = 1,
                        Username = "bravo",
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

    private static FakeMatchQuery CreateMatchQuery() =>
        new(
            recent: new List<PlayerMatch>
            {
                new()
                {
                    AccountId = PlayerA,
                    GameId = 1,
                    HeroId = Hero,
                    Team = "Legion",
                    WinningTeam = "Legion",
                },
                new()
                {
                    AccountId = PlayerA,
                    GameId = 2,
                    HeroId = Hero,
                    Team = "Legion",
                    WinningTeam = "Hellbourne",
                },
            },
            summaries: new Dictionary<int, MatchDetail>
            {
                [1] = new MatchDetail
                {
                    GameId = 1,
                    WinningTeam = "Legion",
                    Players =
                    [
                        Player(PlayerA, Hero, "Legion", [10, 20]),
                        Player(PlayerB, 200, "Legion", [30]),
                        Player(PlayerC, 300, "Hellbourne", [40]),
                    ],
                },
                [2] = new MatchDetail
                {
                    GameId = 2,
                    WinningTeam = "Hellbourne",
                    Players =
                    [
                        Player(PlayerA, Hero, "Legion", [10, 30]),
                        Player(PlayerB, 200, "Legion", [20]),
                        Player(PlayerD, 400, "Hellbourne", [50]),
                    ],
                },
            }
        );

    private static MatchPlayer Player(Guid id, int hero, string team, int[] items) =>
        new()
        {
            AccountId = id,
            Team = team,
            HeroId = hero,
            Inventory = items.ToList(),
        };

    private sealed class FakeMatchQuery(
        IReadOnlyList<PlayerMatch> recent,
        Dictionary<int, MatchDetail> summaries
    ) : IMatchQuery
    {
        public Task<IReadOnlyList<PlayerMatch>> GetRecentForPlayerAsync(
            Guid playerId,
            int limit,
            int offset,
            CancellationToken ct = default
        ) => Task.FromResult(recent);

        public Task<MatchDetail?> GetSummaryAsync(int gameId, CancellationToken ct = default) =>
            Task.FromResult(summaries.TryGetValue(gameId, out var s) ? s : null);
    }

    private sealed class FakeNameResolver(Dictionary<Guid, ResolvedName> names)
        : IPlayerNameResolver
    {
        public Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
            IReadOnlyCollection<Guid> accountIds,
            CancellationToken ct = default
        )
        {
            var result = accountIds
                .Where(names.ContainsKey)
                .ToDictionary(id => id, id => names[id]);
            IReadOnlyDictionary<Guid, ResolvedName> typed = result;
            return Task.FromResult(typed);
        }
    }
}
