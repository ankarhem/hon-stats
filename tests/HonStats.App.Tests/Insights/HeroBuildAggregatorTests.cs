using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class HeroBuildAggregatorTests
{
    [Fact]
    public void EmptyInputs_ReturnsEmpty()
    {
        var result = HeroBuildAggregator.Build([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void CountsItemPresencePerMatch_AndReportsTotalGames()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [2445, 2266, 2284] },
            new() { GameId = 2, ItemIds = [2445, 2266] },
            new() { GameId = 3, ItemIds = [2445, 2490] },
        };

        var result = HeroBuildAggregator.Build(inputs);

        result.Should().HaveCount(4);
        result
            .Should()
            .ContainEquivalentOf(
                new HeroBuildEntry
                {
                    ItemId = 2445,
                    Frequency = 3,
                    Games = 3,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new HeroBuildEntry
                {
                    ItemId = 2266,
                    Frequency = 2,
                    Games = 3,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new HeroBuildEntry
                {
                    ItemId = 2284,
                    Frequency = 1,
                    Games = 3,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new HeroBuildEntry
                {
                    ItemId = 2490,
                    Frequency = 1,
                    Games = 3,
                }
            );
    }

    [Fact]
    public void DedupesDuplicateItemsWithinASingleMatch()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [2445, 2445, 2266] },
        };

        var result = HeroBuildAggregator.Build(inputs);

        result
            .Should()
            .ContainEquivalentOf(
                new HeroBuildEntry
                {
                    ItemId = 2445,
                    Frequency = 1,
                    Games = 1,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new HeroBuildEntry
                {
                    ItemId = 2266,
                    Frequency = 1,
                    Games = 1,
                }
            );
    }

    [Fact]
    public void OrdersByFrequencyDescendingThenItemIdAscending()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [3000, 2000] },
            new() { GameId = 2, ItemIds = [2000, 1000] },
            new() { GameId = 3, ItemIds = [2000] },
        };

        var result = HeroBuildAggregator.Build(inputs);

        result.Select(e => e.ItemId).Should().Equal([2000, 1000, 3000]);
    }
}
