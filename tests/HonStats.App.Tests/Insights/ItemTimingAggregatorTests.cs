using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class ItemTimingAggregatorTests
{
    private static readonly Guid LegionA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LegionB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid HellA = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public void RecordsFirstAppearance_AcrossTimeline()
    {
        var replay = Replay(
            gameId: 100,
            anchor: Anchor(AnchorTeam(LegionA)),
            snapshots:
            [
                Snap(-90, Pos()),
                Snap(120, Pos(Item(7, 0))),
                Snap(300, Pos(Item(7, 0), Item(9, 1))),
            ]
        );

        var result = ItemTimingAggregator.Build(replay);

        result
            .Should()
            .ContainEquivalentOf(
                new ItemBuyTime
                {
                    GameId = 100,
                    AccountId = LegionA,
                    ItemId = 7,
                    FirstSeenSeconds = 120,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new ItemBuyTime
                {
                    GameId = 100,
                    AccountId = LegionA,
                    ItemId = 9,
                    FirstSeenSeconds = 300,
                }
            );
    }

    [Fact]
    public void ItemsPresentAtAnchor_GetEarliestTime()
    {
        var replay = Replay(
            gameId: 1,
            anchor: Anchor(AnchorTeam(LegionA)),
            snapshots: [Snap(-90, Pos(Item(5, 0))), Snap(0, Pos(Item(5, 0), Item(6, 1)))]
        );

        var result = ItemTimingAggregator.Build(replay);

        result.Should().Contain(t => t.ItemId == 5 && t.FirstSeenSeconds == -90);
        result.Should().Contain(t => t.ItemId == 6 && t.FirstSeenSeconds == 0);
    }

    [Fact]
    public void MapsPositionToAccountId_ViaSnapshotZeroAnchor()
    {
        var replay = Replay(
            gameId: 1,
            anchor: Anchor(AnchorTeam(LegionA, LegionB), AnchorTeam(HellA)),
            Snap(0, Pos(Item(11, 0)), Pos(Item(22, 0)))
        );

        var result = ItemTimingAggregator.Build(replay);

        result
            .Should()
            .ContainEquivalentOf(
                new ItemBuyTime
                {
                    GameId = 1,
                    AccountId = LegionA,
                    ItemId = 11,
                    FirstSeenSeconds = 0,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new ItemBuyTime
                {
                    GameId = 1,
                    AccountId = HellA,
                    ItemId = 22,
                    FirstSeenSeconds = 0,
                }
            );
        result.Should().NotContain(t => t.AccountId == LegionB);
    }

    [Fact]
    public void ReappearingItem_KeepsFirstAppearanceTime()
    {
        var replay = Replay(
            gameId: 1,
            anchor: Anchor(AnchorTeam(LegionA)),
            snapshots: [Snap(60, Pos(Item(7, 0))), Snap(120, Pos()), Snap(240, Pos(Item(7, 0)))]
        );

        var result = ItemTimingAggregator.Build(replay);

        result.Should().ContainSingle(t => t.ItemId == 7);
        result.Single(t => t.ItemId == 7).FirstSeenSeconds.Should().Be(60);
    }

    [Fact]
    public void EmptyReplay_ReturnsEmpty()
    {
        var replay = new ParsedReplay { GameId = 1 };

        ItemTimingAggregator.Build(replay).Should().BeEmpty();
    }

    [Fact]
    public void AnchorWithoutAccountIds_ReturnsEmpty()
    {
        var replay = new ParsedReplay
        {
            GameId = 1,
            Snapshots =
            [
                new() { Time = 0, Teams = [new() { Players = [new() { Items = [Item(7, 0)] }] }] },
            ],
        };

        ItemTimingAggregator.Build(replay).Should().BeEmpty();
    }

    [Fact]
    public void IgnoresSentinelItemIds()
    {
        var replay = Replay(
            gameId: 1,
            anchor: Anchor(AnchorTeam(LegionA)),
            Snap(0, Pos(Item(0, 0), Item(65535, 1), Item(42, 2)))
        );

        var result = ItemTimingAggregator.Build(replay);

        result.Should().ContainSingle(t => t.ItemId == 42);
    }

    [Fact]
    public void Fixture_MapsAnchoredPlayersAndIgnoresSentinels()
    {
        var replay = FixtureReplay();

        var result = ItemTimingAggregator.Build(replay);

        result.Should().NotBeEmpty();
        var anchored = replay
            .Snapshots[0]
            .Teams.SelectMany(t => t.Players)
            .Select(p => p.AccountId)
            .Where(a => a.HasValue)
            .Select(a => a!.Value)
            .ToHashSet();
        result.Select(t => t.AccountId).ToHashSet().Should().BeSubsetOf(anchored);
        result.Should().NotContain(t => t.ItemId == 0);
        result.Should().NotContain(t => t.ItemId == 65535);
        // The fixture only has pre-game snapshots (-90/-60/-30).
        result.Should().OnlyContain(t => t.FirstSeenSeconds <= 0);
    }

    private static ParsedReplay FixtureReplay()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "parsedreplay.json");
        var json = File.ReadAllText(path);
        return System
            .Text.Json.JsonSerializer.Deserialize<ParsedReplayFixture>(json, FixtureOptions)!
            .ParsedReplay!;
    }

    private static readonly System.Text.Json.JsonSerializerOptions FixtureOptions = new(
        System.Text.Json.JsonSerializerDefaults.Web
    );

    private sealed class ParsedReplayFixture
    {
        public ParsedReplay? ParsedReplay { get; set; }
    }

    private static ParsedReplay Replay(
        int gameId,
        ReplaySnapshot anchor,
        params ReplaySnapshot[] snapshots
    ) => new() { GameId = gameId, Snapshots = [anchor, .. snapshots] };

    private static ReplaySnapshot Anchor(params ReplayTeam[] teams) =>
        new() { Time = -90, Teams = teams.ToList() };

    // An anchor team: positional players that carry their AccountId (snapshot[0] shape).
    private static ReplayTeam AnchorTeam(params Guid[] accounts) =>
        new() { Players = accounts.Select(a => new ReplayPlayer { AccountId = a }).ToList() };

    private static ReplaySnapshot Snap(int time, params ReplayTeam[] teams) =>
        new() { Time = time, Teams = teams.ToList() };

    // A positional team in a non-anchor snapshot: one player with items (no AccountId).
    private static ReplayTeam Pos(params ReplayItem[] items) =>
        new() { Players = [new() { Items = items.ToList() }] };

    private static ReplayItem Item(int itemId, int slot) => new() { ItemId = itemId, Slot = slot };
}
