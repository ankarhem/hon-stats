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
            services.AddSingleton<IPlayerRatingsQuery>(new FakeRatingsQuery());
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

            var dbFactory2 = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>();
            await using var db2 = dbFactory2.CreateDbContext();
            var snapshots = await db2.MmrSnapshots.Where(s => s.AccountId == PlayerA).ToListAsync();
            snapshots.Should().HaveCount(1);
            snapshots[0].RankedCaldavarRating.Should().Be(1600.0);
            snapshots[0].RankedMidwarsRating.Should().Be(1700.0);

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
            services.AddSingleton<IPlayerRatingsQuery>(new FakeRatingsQuery());
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

    [Fact]
    public async Task Index_Twice_On_SamePlayer_ForceBackfill_ReplacesRosterAndItems()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-force-{Guid.NewGuid():N}.db");
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
            services.AddSingleton<IPlayerRatingsQuery>(new FakeRatingsQuery());
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

            // Cold index — ingests games 1+2, fetches each summary once.
            await indexer.IndexAsync(PlayerA);
            var callsAfterCold = matchQuery.SummaryCalls;
            callsAfterCold.Should().BeGreaterThan(0);

            int rosterAfterCold;
            int itemsAfterCold;
            await using (
                var db1 = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>()
                    .CreateDbContext()
            )
            {
                rosterAfterCold = await db1.MatchRoster.CountAsync();
                itemsAfterCold = await db1.MatchPlayerItems.CountAsync();
            }
            rosterAfterCold.Should().BeGreaterThan(0);
            itemsAfterCold.Should().BeGreaterThan(0);

            // Force-backfill reindex — same fake match query, no new games.
            await indexer.IndexAsync(PlayerA, forceBackfill: true);

            // RED on current code: summaries are never re-fetched (cursor +
            // alreadyIngested gates short-circuit the entire pipeline).
            matchQuery.SummaryCalls.Should().BeGreaterThan(callsAfterCold);

            // Roster/items are replaced (delete-then-insert), not duplicated.
            await using (
                var db2 = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>()
                    .CreateDbContext()
            )
            {
                (await db2.MatchRoster.CountAsync()).Should().Be(rosterAfterCold);
                (await db2.MatchPlayerItems.CountAsync()).Should().Be(itemsAfterCold);
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
    public async Task Index_DegenerateReplay_WritesNoTimingRows()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-degenerate-{Guid.NewGuid():N}.db");
        try
        {
            var matchQuery = CreateMatchQuery();
            // Single-snapshot replay — degenerate (replay not ready yet)
            var replayQuery = new FakeParsedReplayQuery(
                new Dictionary<int, ParsedReplay>
                {
                    [1] = new ParsedReplay
                    {
                        GameId = 1,
                        Snapshots =
                        [
                            new()
                            {
                                Time = 300,
                                Teams =
                                [
                                    new()
                                    {
                                        Players =
                                        [
                                            new()
                                            {
                                                AccountId = PlayerA,
                                                Items = [new() { ItemId = 10 }],
                                            },
                                        ],
                                    },
                                ],
                            },
                        ],
                    },
                    [2] = new ParsedReplay
                    {
                        GameId = 2,
                        Snapshots =
                        [
                            new()
                            {
                                Time = 300,
                                Teams =
                                [
                                    new()
                                    {
                                        Players =
                                        [
                                            new()
                                            {
                                                AccountId = PlayerA,
                                                Items = [new() { ItemId = 20 }],
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
            services.AddSingleton<IPlayerRatingsQuery>(new FakeRatingsQuery());
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
                File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task Index_ReprocessesStaleItemTimings_OnReindex()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-reprocess-{Guid.NewGuid():N}.db");
        try
        {
            var matchQuery = CreateMatchQuery();
            // Healthy replay now available for game 1 — item 10 at 120s, item 20 at 240s
            var replayQuery = new FakeParsedReplayQuery(
                new Dictionary<int, ParsedReplay>
                {
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
            services.AddSingleton<IPlayerRatingsQuery>(new FakeRatingsQuery());
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

            // Migrate + seed pre-fix state: game 1 fully indexed with DEGENERATE timing
            // data (all items share one FirstSeenSeconds — the fingerprint).
            await using (
                var seedDb = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>()
                    .CreateDbContext()
            )
            {
                await seedDb.Database.MigrateAsync();

                seedDb.IndexedPlayers.Add(
                    new IndexedPlayer
                    {
                        AccountId = PlayerA,
                        LastIndexedMatchId = 2,
                        Status = IndexingStatus.Indexed,
                    }
                );
                seedDb.PlayerMatches.Add(
                    new PlayerMatch
                    {
                        AccountId = PlayerA,
                        GameId = 1,
                        HeroId = Hero,
                        Team = "Legion",
                        WinningTeam = "Legion",
                    }
                );
                seedDb.PlayerMatches.Add(
                    new PlayerMatch
                    {
                        AccountId = PlayerA,
                        GameId = 2,
                        HeroId = Hero,
                        Team = "Legion",
                        WinningTeam = "Hellbourne",
                    }
                );
                seedDb.MatchRoster.Add(
                    new MatchRoster
                    {
                        GameId = 1,
                        AccountId = PlayerA,
                        Team = "Legion",
                        Won = true,
                    }
                );
                seedDb.MatchRoster.Add(
                    new MatchRoster
                    {
                        GameId = 2,
                        AccountId = PlayerA,
                        Team = "Legion",
                        Won = false,
                    }
                );
                // Degenerate fingerprint: both items at the SAME timestamp
                seedDb.MatchItemTimings.Add(
                    new MatchItemTiming
                    {
                        GameId = 1,
                        AccountId = PlayerA,
                        ItemId = 10,
                        FirstSeenSeconds = 300,
                    }
                );
                seedDb.MatchItemTimings.Add(
                    new MatchItemTiming
                    {
                        GameId = 1,
                        AccountId = PlayerA,
                        ItemId = 20,
                        FirstSeenSeconds = 300,
                    }
                );
                await seedDb.SaveChangesAsync();
            }

            using var scope = sp.CreateScope();
            var indexer = scope.ServiceProvider.GetRequiredService<IPlayerInsightsIndexer>();

            // Normal reindex — no new games, but ReprocessStaleItemTimingsAsync
            // should detect game 1's degenerate fingerprint and re-fetch the replay.
            await indexer.IndexAsync(PlayerA);

            await using (
                var assertDb = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>()
                    .CreateDbContext()
            )
            {
                var timings = await assertDb
                    .MatchItemTimings.Where(t => t.GameId == 1)
                    .ToListAsync();

                timings.Should().HaveCount(2);
                timings.Select(t => t.FirstSeenSeconds).Distinct().Should().HaveCountGreaterThan(1);
                timings
                    .Should()
                    .ContainEquivalentOf(
                        new
                        {
                            GameId = 1,
                            AccountId = PlayerA,
                            ItemId = 10,
                            FirstSeenSeconds = 120,
                        }
                    );
                timings
                    .Should()
                    .ContainEquivalentOf(
                        new
                        {
                            GameId = 1,
                            AccountId = PlayerA,
                            ItemId = 20,
                            FirstSeenSeconds = 240,
                        }
                    );
            }
        }
        finally
        {
            if (File.Exists(dbPath))
                File.Delete(dbPath);
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
        public int SummaryCalls;

        public Task<IReadOnlyList<PlayerMatch>> GetRecentForPlayerAsync(
            Guid playerId,
            int limit,
            int offset,
            CancellationToken ct = default
        ) => Task.FromResult(recent);

        public Task<MatchDetail?> GetSummaryAsync(int gameId, CancellationToken ct = default)
        {
            SummaryCalls++;
            return Task.FromResult(summaries.TryGetValue(gameId, out var s) ? s : null);
        }
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

    private sealed class FakeRatingsQuery : IPlayerRatingsQuery
    {
        public Task<PlayerRatings?> GetAsync(Guid accountId, CancellationToken ct = default)
        {
            return Task.FromResult<PlayerRatings?>(new PlayerRatings(1500.0, 1600.0, 1700.0));
        }
    }
}
