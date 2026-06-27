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
using HonStats.Infra.Players;
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
            services.AddSingleton<IParsedReplayQuery>(new ThrowingParsedReplayQuery());
            services.AddSingleton<IPlayerNameStore, SqlitePlayerNameStore>();
            services.AddSingleton<IPlayerNameResolver>(sp => new LocalFirstPlayerNameResolver(
                nameResolver,
                sp.GetRequiredService<IPlayerNameStore>()
            ));
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

            var build = await query.GetHeroBuildAsync(PlayerA, Hero);
            build
                .Should()
                .ContainEquivalentOf(
                    new HeroBuildEntry
                    {
                        ItemId = 10,
                        Frequency = 2,
                        Wins = 1,
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
                        Wins = 1,
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
                        Wins = 0,
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

            // Replay ingest is unconditional, but replay failures are isolated and must
            // not break core ingestion; no timing rows are written when the replay query fails.
            await using (
                var assertDb = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>()
                    .CreateDbContext()
            )
            {
                (await assertDb.MatchItemTimings.ToListAsync()).Should().BeEmpty();
            }
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task Index_PersistsItemTiming_AndReadsAverage()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-timing-{Guid.NewGuid():N}.db");
        try
        {
            var matchQuery = CreateMatchQuery();
            var replayQuery = new FakeParsedReplayQuery(
                new Dictionary<int, ParsedReplay>
                {
                    // snapshot[0] anchors PlayerA; item 10 first seen at t=120s, item 20 at t=240s.
                    [1] = new ParsedReplay
                    {
                        GameId = 1,
                        Snapshots =
                        [
                            new()
                            {
                                Time = -90,
                                Teams = [new() { Players = [new() { AccountId = PlayerA }] }],
                            },
                            new()
                            {
                                Time = 120,
                                Teams =
                                [
                                    new()
                                    {
                                        Players =
                                        [
                                            new() { Items = [new() { ItemId = 10, Slot = 0 }] },
                                        ],
                                    },
                                ],
                            },
                            new()
                            {
                                Time = 240,
                                Teams =
                                [
                                    new()
                                    {
                                        Players =
                                        [
                                            new()
                                            {
                                                Items =
                                                [
                                                    new() { ItemId = 10, Slot = 0 },
                                                    new() { ItemId = 20, Slot = 1 },
                                                ],
                                            },
                                        ],
                                    },
                                ],
                            },
                        ],
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
            services.AddSingleton<IParsedReplayQuery>(replayQuery);
            services.AddSingleton<IPlayerNameStore, SqlitePlayerNameStore>();
            services.AddSingleton<IPlayerNameResolver>(sp => new LocalFirstPlayerNameResolver(
                new FakeNameResolver(
                    new Dictionary<Guid, ResolvedName>
                    {
                        [PlayerA] = new()
                        {
                            AccountId = PlayerA,
                            Username = "alpha",
                            Country = "SE",
                        },
                    }
                ),
                sp.GetRequiredService<IPlayerNameStore>()
            ));
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
            var timing = await query.GetHeroItemTimingAsync(PlayerA, Hero);

            timing
                .Should()
                .ContainEquivalentOf(
                    new ItemTimingEntry
                    {
                        ItemId = 10,
                        AvgSeconds = 120,
                        Games = 1,
                    }
                );
            timing
                .Should()
                .ContainEquivalentOf(
                    new ItemTimingEntry
                    {
                        ItemId = 20,
                        AvgSeconds = 240,
                        Games = 1,
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

    private sealed class ThrowingParsedReplayQuery : IParsedReplayQuery
    {
        public Task<ParsedReplay?> GetAsync(int gameId, CancellationToken ct = default) =>
            throw new InvalidOperationException("replay query failure must be isolated");
    }

    private sealed class FakeParsedReplayQuery(Dictionary<int, ParsedReplay> replays)
        : IParsedReplayQuery
    {
        public Task<ParsedReplay?> GetAsync(int gameId, CancellationToken ct = default) =>
            Task.FromResult(replays.TryGetValue(gameId, out var r) ? r : null);
    }
}
