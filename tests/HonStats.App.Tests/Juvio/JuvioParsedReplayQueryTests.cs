using System.Net;
using AwesomeAssertions;
using HonStats.App.Matches;
using HonStats.Infra.Juvio;
using HonStats.Infra.Juvio.Adapters;
using Xunit;

namespace HonStats.App.Tests.Juvio;

public class JuvioParsedReplayQueryTests
{
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "parsedreplay.json"
    );

    [Fact]
    public async Task DeserializesAndMapsFixture()
    {
        var json = await File.ReadAllTextAsync(FixturePath);
        var query = QueryReturning(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
        );

        var replay = await query.GetAsync(8008220);

        replay.Should().NotBeNull();
        replay!.GameId.Should().Be(8008220);
        replay.WinningTeam.Should().Be("Legion");
        replay.Snapshots.Should().NotBeEmpty();

        var snapshot0 = replay.Snapshots[0];
        snapshot0.Time.Should().Be(-90);
        snapshot0.Teams.Should().HaveCount(2);

        var anchoredPlayers = snapshot0
            .Teams.SelectMany(t => t.Players)
            .Where(p => p.SlotIndex is not null)
            .ToList();
        anchoredPlayers.Should().NotBeEmpty();
        anchoredPlayers.Should().OnlyContain(p => p.StartingGold != null);

        var itemizedPlayer = replay
            .Snapshots.SelectMany(s => s.Teams)
            .SelectMany(t => t.Players)
            .First(p => p.Items.Count > 0);
        itemizedPlayer.Items.Should().OnlyContain(i => i.Slot >= 0);
    }

    [Fact]
    public async Task MapsSkillsAndDamageFields_OnPopulatedSnapshot()
    {
        const string json = """
            {
              "parsedReplay": {
                "gameId": 8008220,
                "winningTeam": "Legion",
                "snapshots": [
                  {
                    "time": 600,
                    "teams": [
                      {
                        "players": [
                          {
                            "netWorth": 5000,
                            "skills": [ { "skillId": 1017, "level": 3 } ],
                            "heroDamage": 12345,
                            "buildingDamage": 678,
                            "creepKills": 42
                          }
                        ]
                      }
                    ]
                  }
                ]
              }
            }
            """;
        var query = QueryReturning(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
        );

        var replay = await query.GetAsync(8008220);

        replay.Should().NotBeNull();
        var player = replay!.Snapshots[0].Teams[0].Players[0];
        player.Skills.Should().ContainSingle();
        player.Skills[0].SkillId.Should().Be(1017);
        player.Skills[0].Level.Should().Be(3);
        player.HeroDamage.Should().Be(12345);
        player.BuildingDamage.Should().Be(678);
        player.CreepKills.Should().Be(42);
    }

    [Fact]
    public async Task DefaultsSkillsToEmpty_WhenAbsent()
    {
        const string json = """
            {
              "parsedReplay": {
                "gameId": 1,
                "snapshots": [
                  { "time": 0, "teams": [ { "players": [ { "netWorth": 100 } ] } ] }
                ]
              }
            }
            """;
        var query = QueryReturning(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }
        );

        var replay = await query.GetAsync(1);

        var player = replay!.Snapshots[0].Teams[0].Players[0];
        player.Skills.Should().NotBeNull();
        player.Skills.Should().BeEmpty();
        player.HeroDamage.Should().BeNull();
    }

    [Fact]
    public async Task ReturnsNull_OnNotFound()
    {
        var query = QueryReturning(new HttpResponseMessage(HttpStatusCode.NotFound));

        var replay = await query.GetAsync(1);

        replay.Should().BeNull();
    }

    private static IParsedReplayQuery QueryReturning(HttpResponseMessage response) =>
        new JuvioParsedReplayQuery(new StubHttpClientFactory(response));

    private sealed class StubHttpClientFactory(HttpResponseMessage response) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new StubHandler(response)) { BaseAddress = new Uri("https://stats.juvio.com") };
    }

    private sealed class StubHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(response);
    }
}
