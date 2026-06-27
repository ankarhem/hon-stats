using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class HeroItemPairAggregatorTests
{
    [Fact]
    public void EmptyInputs_ReturnsEmpty()
    {
        var result = HeroItemPairAggregator.Build([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void SingleItemMatch_ProducesNoPairs()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [2445] },
        };

        var result = HeroItemPairAggregator.Build(inputs);

        result.Should().BeEmpty();
    }

    [Fact]
    public void PairsAreCanonicalized_ItemAAlwaysLessThanItemB()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [2266, 2445] },
        };

        var result = HeroItemPairAggregator.Build(inputs);

        result.Should().HaveCount(1);
        result[0].ItemA.Should().Be(2266);
        result[0].ItemB.Should().Be(2445);
    }

    [Fact]
    public void DedupesDuplicateItemsWithinASingleMatch_NoSelfPairs()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [2445, 2445, 2266] },
        };

        var result = HeroItemPairAggregator.Build(inputs);

        result.Should().HaveCount(1);
        result
            .Should()
            .ContainEquivalentOf(
                new HeroItemPairEntry
                {
                    ItemA = 2266,
                    ItemB = 2445,
                    Frequency = 1,
                    Games = 1,
                }
            );
        result.Should().NotContain(p => p.ItemA == p.ItemB);
    }

    [Fact]
    public void CountsCoOccurrenceAcrossMatches_AndReportsTotalGames()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [10, 20] },
            new() { GameId = 2, ItemIds = [10, 20, 30] },
            new() { GameId = 3, ItemIds = [10, 30] },
            new() { GameId = 4, ItemIds = [40] },
        };

        var result = HeroItemPairAggregator.Build(inputs);

        result.Should().HaveCount(3);
        result
            .Should()
            .ContainEquivalentOf(
                new HeroItemPairEntry
                {
                    ItemA = 10,
                    ItemB = 20,
                    Frequency = 2,
                    Games = 4,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new HeroItemPairEntry
                {
                    ItemA = 10,
                    ItemB = 30,
                    Frequency = 2,
                    Games = 4,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new HeroItemPairEntry
                {
                    ItemA = 20,
                    ItemB = 30,
                    Frequency = 1,
                    Games = 4,
                }
            );
    }

    [Fact]
    public void OrdersByFrequencyDescendingThenItemAThenItemB()
    {
        var inputs = new List<MatchItemInput>
        {
            new() { GameId = 1, ItemIds = [100, 200] },
            new() { GameId = 2, ItemIds = [100, 200] },
            new() { GameId = 3, ItemIds = [100, 300] },
            new() { GameId = 4, ItemIds = [100, 300] },
            new() { GameId = 5, ItemIds = [200, 300] },
        };

        var result = HeroItemPairAggregator.Build(inputs);

        // (100,200) and (100,300) both freq 2; (200,300) freq 1.
        // Tie broken by ItemA then ItemB: (100,200) before (100,300).
        result
            .Select(p => (p.ItemA, p.ItemB))
            .Should()
            .Equal([(100, 200), (100, 300), (200, 300)]);
    }
}
