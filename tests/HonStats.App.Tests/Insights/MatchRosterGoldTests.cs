using System.Text.Json;
using AwesomeAssertions;
using HonStats.App.Events;
using HonStats.App.Insights;
using HonStats.App.Matches;
using HonStats.App.Players;
using HonStats.Domain.Events;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Infra.Insights;
using HonStats.Infra.Juvio.Adapters;
using HonStats.Infra.Persistence;
using HonStats.Infra.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class MatchRosterGoldTests
{
    private static readonly Guid PlayerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PlayerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void GetMatchSummary_DeserializesGoldFields_TolerantOfMixedCasing()
    {
        var json = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Insights", "Fixtures", "getmatchsummary.json")
        );

        var dto = JsonSerializer.Deserialize<MatchSummaryDto>(json, JuvioJson.Options);

        dto.Should().NotBeNull();
        var first = dto!.PlayersValue[0];
        first.NetWorth.Should().Be(14500);
        first.GoldFromCreeps.Should().Be(6200);
        first.GoldFromNeutrals.Should().Be(1450);
        first.GoldFromKills.Should().Be(2100);
        first.GoldFromAssists.Should().Be(1800);
        first.GoldFromBuildings.Should().Be(950);
        first.StartingGold.Should().Be(625);
        first.DeathGoldLost.Should().Be(430);
    }

    [Fact]
    public async Task Index_WritesGoldFields_OntoMatchRosterRow()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"honstats-gold-{Guid.NewGuid():N}.db");
        try
        {
            var matchQuery = new FakeMatchQuery(
                recent: new List<PlayerMatch>
                {
                    new()
                    {
                        AccountId = PlayerA,
                        GameId = 1,
                        HeroId = 100,
                        Team = "Legion",
                        WinningTeam = "Legion",
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
                            new MatchPlayer
                            {
                                AccountId = PlayerA,
                                Team = "Legion",
                                HeroId = 100,
                                NetWorth = 14500,
                                GoldFromCreeps = 6200,
                                GoldFromNeutrals = 1450,
                                GoldFromKills = 2100,
                                GoldFromAssists = 1800,
                                GoldFromBuildings = 950,
                                StartingGold = 625,
                                DeathGoldLost = 430,
                                Inventory = [10, 20],
                            },
                            new MatchPlayer
                            {
                                AccountId = PlayerB,
                                Team = "Hellbourne",
                                HeroId = 200,
                                Inventory = [30],
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
            services.AddSingleton<IPlayerNameStore, SqlitePlayerNameStore>();
            services.AddSingleton<IPlayerNameResolver>(sp => new LocalFirstPlayerNameResolver(
                new FakeNameResolver(),
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

            await using var verify = sp.GetRequiredService<IDbContextFactory<HonStatsDbContext>>()
                .CreateDbContext();
            var roster = await verify.MatchRoster.SingleAsync(r =>
                r.GameId == 1 && r.AccountId == PlayerA
            );

            roster.GoldFromCreeps.Should().Be(6200);
            roster.GoldFromNeutrals.Should().Be(1450);
            roster.GoldFromKills.Should().Be(2100);
            roster.GoldFromAssists.Should().Be(1800);
            roster.GoldFromBuildings.Should().Be(950);
            roster.StartingGold.Should().Be(625);
            roster.DeathGoldLost.Should().Be(430);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

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

    private sealed class FakeNameResolver : IPlayerNameResolver
    {
        public Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
            IReadOnlyCollection<Guid> accountIds,
            CancellationToken ct = default
        )
        {
            IReadOnlyDictionary<Guid, ResolvedName> empty = new Dictionary<Guid, ResolvedName>();
            return Task.FromResult(empty);
        }
    }
}
