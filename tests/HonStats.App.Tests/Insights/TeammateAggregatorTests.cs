using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class TeammateAggregatorTests
{
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Carol = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void EmptyInputs_ReturnsEmpty()
    {
        var result = TeammateAggregator.Build([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void CountsGamesAndWinsTogetherAcrossMatches()
    {
        var inputs = new List<MatchTeammateInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                TeammateAccountIds = [Alice, Bob],
            },
            new()
            {
                GameId = 2,
                Won = true,
                TeammateAccountIds = [Alice],
            },
            new()
            {
                GameId = 3,
                Won = false,
                TeammateAccountIds = [Alice, Carol],
            },
        };

        var result = TeammateAggregator.Build(inputs);

        result
            .Should()
            .ContainEquivalentOf(
                new TeammateAggregate
                {
                    TeammateAccountId = Alice,
                    GamesTogether = 3,
                    WinsTogether = 2,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new TeammateAggregate
                {
                    TeammateAccountId = Bob,
                    GamesTogether = 1,
                    WinsTogether = 1,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new TeammateAggregate
                {
                    TeammateAccountId = Carol,
                    GamesTogether = 1,
                    WinsTogether = 0,
                }
            );
    }

    [Fact]
    public void DedupesSameTeammateWithinASingleMatch()
    {
        var inputs = new List<MatchTeammateInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                TeammateAccountIds = [Alice, Alice],
            },
        };

        var result = TeammateAggregator.Build(inputs);

        result
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new TeammateAggregate
                {
                    TeammateAccountId = Alice,
                    GamesTogether = 1,
                    WinsTogether = 1,
                }
            );
    }

    [Fact]
    public void OrdersByGamesTogetherDescendingThenAccountIdAscending()
    {
        var inputs = new List<MatchTeammateInput>
        {
            new()
            {
                GameId = 1,
                Won = true,
                TeammateAccountIds = [Bob, Carol],
            },
            new()
            {
                GameId = 2,
                Won = false,
                TeammateAccountIds = [Carol],
            },
        };

        var result = TeammateAggregator.Build(inputs);

        result.Select(t => t.TeammateAccountId).Should().Equal([Carol, Bob]);
    }
}
