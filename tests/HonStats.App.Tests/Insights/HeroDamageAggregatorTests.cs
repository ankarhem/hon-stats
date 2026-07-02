using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Matches;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class HeroDamageAggregatorTests
{
    private static readonly Guid LegionA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid HellA = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public void ReturnsPerAccountMax_HeroDamage_AcrossSnapshots()
    {
        var replay = Replay(
            gameId: 1,
            anchor: Anchor(AnchorTeam(LegionA), AnchorTeam(HellA)),
            Snap(30, Dmg(500), Dmg(100)),
            Snap(120, Dmg(1800), Dmg(900)),
            Snap(300, Dmg(5000), Dmg(null))
        );

        var result = HeroDamageAggregator.Build(replay);

        result.Should().HaveCount(2);
        result[LegionA].Should().Be(5000);
        result[HellA].Should().Be(900);
    }

    [Fact]
    public void NullReplay_ReturnsEmpty()
    {
        HeroDamageAggregator.Build(null).Should().BeEmpty();
    }

    [Fact]
    public void EmptyReplay_ReturnsEmpty()
    {
        HeroDamageAggregator.Build(new ParsedReplay { GameId = 1 }).Should().BeEmpty();
    }

    [Fact]
    public void AnchorWithoutAccountIds_ReturnsEmpty()
    {
        var replay = new ParsedReplay
        {
            GameId = 1,
            Snapshots =
            [
                new() { Time = 0, Teams = [new() { Players = [new() { HeroDamage = 100 }] }] },
            ],
        };

        HeroDamageAggregator.Build(replay).Should().BeEmpty();
    }

    private static ParsedReplay Replay(
        int gameId,
        ReplaySnapshot anchor,
        params ReplaySnapshot[] snapshots
    ) => new() { GameId = gameId, Snapshots = [anchor, .. snapshots] };

    private static ReplaySnapshot Anchor(params ReplayTeam[] teams) =>
        new() { Time = -90, Teams = teams.ToList() };

    private static ReplayTeam AnchorTeam(params Guid[] accounts) =>
        new() { Players = accounts.Select(a => new ReplayPlayer { AccountId = a }).ToList() };

    private static ReplaySnapshot Snap(int time, params ReplayTeam[] teams) =>
        new() { Time = time, Teams = teams.ToList() };

    private static ReplayTeam Dmg(int? heroDamage) =>
        new() { Players = [new() { HeroDamage = heroDamage }] };
}
