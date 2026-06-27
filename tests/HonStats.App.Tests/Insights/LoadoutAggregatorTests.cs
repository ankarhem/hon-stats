using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class LoadoutAggregatorTests
{
    // A "loadout" = the set of non-consumable items in a player's final inventory
    // for a given match, reduced across matches to loadout -> count + win rate.
    // Consumable item ids are supplied by the caller (resolved from gamedata in
    // production); the aggregator is pure and treats them as an opaque id set.

    [Fact]
    public void Build_EmptyInputs_ReturnsEmptyList()
    {
        var result = LoadoutAggregator.Build([], new HashSet<int>());

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true, 1.0)]
    [InlineData(false, 0.0)]
    public void Build_SingleMatch_SingleLoadout_Count1_Winrate1Or0(bool won, double expectedWinRate)
    {
        var inputs = new List<MatchItemInput>
        {
            new()
            {
                GameId = 1,
                Won = won,
                ItemIds = [1, 2],
            },
        };

        var result = LoadoutAggregator.Build(inputs, new HashSet<int>());

        result.Should().HaveCount(1);
        var entry = result[0];
        entry.ItemIds.Should().Equal([1, 2]);
        entry.Count.Should().Be(1);
        entry.Wins.Should().Be(won ? 1 : 0);
        entry.WinRate.Should().Be(expectedWinRate);
    }

    [Fact]
    public void Build_IdenticalLoadouts_AreGrouped_Count2()
    {
        var inputs = new List<MatchItemInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                ItemIds = [1, 2],
            },
            new()
            {
                GameId = 2,
                Won = true,
                ItemIds = [1, 2],
            },
        };

        var result = LoadoutAggregator.Build(inputs, new HashSet<int>());

        result.Should().HaveCount(1);
        var entry = result[0];
        entry.ItemIds.Should().Equal([1, 2]);
        entry.Count.Should().Be(2);
        entry.Wins.Should().Be(2);
    }

    [Fact]
    public void Build_ConsumableItems_AreFiltered()
    {
        // Item 99 is a consumable: it must be stripped before keying the loadout.
        var inputs = new List<MatchItemInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                ItemIds = [1, 2, 99],
            },
        };
        var consumables = new HashSet<int> { 99 };

        var result = LoadoutAggregator.Build(inputs, consumables);

        result.Should().HaveCount(1);
        result[0].ItemIds.Should().Equal([1, 2]);
        result[0].Count.Should().Be(1);
    }

    [Fact]
    public void Build_DifferentSlotOrder_SameItems_CollapseIntoOneLoadout()
    {
        // Slot order is irrelevant: the loadout is a set, canonicalized by sorting.
        var inputs = new List<MatchItemInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                ItemIds = [3, 1, 2],
            },
            new()
            {
                GameId = 2,
                Won = false,
                ItemIds = [1, 2, 3],
            },
        };

        var result = LoadoutAggregator.Build(inputs, new HashSet<int>());

        result.Should().HaveCount(1);
        result[0].ItemIds.Should().Equal([1, 2, 3]);
        result[0].Count.Should().Be(2);
    }

    [Fact]
    public void Build_AllConsumables_SkippedFromLoadouts()
    {
        // Every item is a consumable: the resulting loadout is empty and must be
        // dropped entirely (no entry emitted).
        var inputs = new List<MatchItemInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                ItemIds = [99, 100],
            },
        };
        var consumables = new HashSet<int> { 99, 100 };

        var result = LoadoutAggregator.Build(inputs, consumables);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Build_OrdersByCountDescending()
    {
        // Three distinct loadouts appearing 3, 1, and 2 times respectively.
        var inputs = new List<MatchItemInput>
        {
            // Loadout {1, 2} x3 (count 3, expected first)
            new() { GameId = 1, ItemIds = [1, 2] },
            new() { GameId = 2, ItemIds = [1, 2] },
            new() { GameId = 3, ItemIds = [1, 2] },
            // Loadout {5, 6} x1 (count 1, expected last)
            new() { GameId = 4, ItemIds = [5, 6] },
            // Loadout {3, 4} x2 (count 2, expected middle)
            new() { GameId = 5, ItemIds = [3, 4] },
            new() { GameId = 6, ItemIds = [3, 4] },
        };

        var result = LoadoutAggregator.Build(inputs, new HashSet<int>());

        result.Should().HaveCount(3);
        result[0].ItemIds.Should().Equal([1, 2]);
        result[0].Count.Should().Be(3);
        result[1].ItemIds.Should().Equal([3, 4]);
        result[1].Count.Should().Be(2);
        result[2].ItemIds.Should().Equal([5, 6]);
        result[2].Count.Should().Be(1);
    }

    [Fact]
    public void Build_CapsAtTop5()
    {
        // Six distinct single-item loadouts with strictly decreasing counts:
        //   {1} x6, {2} x5, {3} x4, {4} x3, {5} x2, {6} x1
        // The cap keeps only the top 5 by count; {6} (lowest) is dropped.
        var inputs = new List<MatchItemInput>();
        for (var itemId = 1; itemId <= 6; itemId++)
        {
            var occurrences = 7 - itemId;
            for (var i = 0; i < occurrences; i++)
            {
                inputs.Add(new MatchItemInput { GameId = itemId * 1000 + i, ItemIds = [itemId] });
            }
        }

        var result = LoadoutAggregator.Build(inputs, new HashSet<int>());

        result.Should().HaveCount(5);
        result[0].ItemIds.Should().Equal([1]);
        result[4].ItemIds.Should().Equal([5]);
        result.Should().NotContain(e => e.ItemIds.SequenceEqual(new[] { 6 }));
    }

    [Fact]
    public void Build_ComputesWinrate()
    {
        // One loadout across 4 matches, 3 of which were won -> WinRate = 0.75.
        var inputs = new List<MatchItemInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                ItemIds = [1, 2],
            },
            new()
            {
                GameId = 2,
                Won = true,
                ItemIds = [1, 2],
            },
            new()
            {
                GameId = 3,
                Won = true,
                ItemIds = [1, 2],
            },
            new()
            {
                GameId = 4,
                Won = false,
                ItemIds = [1, 2],
            },
        };

        var result = LoadoutAggregator.Build(inputs, new HashSet<int>());

        result.Should().HaveCount(1);
        var entry = result[0];
        entry.Count.Should().Be(4);
        entry.Wins.Should().Be(3);
        entry.WinRate.Should().Be(0.75);
    }
}
