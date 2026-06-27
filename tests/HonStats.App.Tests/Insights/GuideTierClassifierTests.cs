using AwesomeAssertions;
using HonStats.App.Insights;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class GuideTierClassifierTests
{
    private static readonly (int ItemId, int TotalCost, double PickRate)[] Empty = [];

    [Fact]
    public void EmptySet_ReturnsEmpty()
    {
        var result = GuideTierClassifier.ClassifySet(Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ItemsAtOrBelowEarlyCutoff_AreEarly()
    {
        var items = new (int ItemId, int TotalCost, double PickRate)[]
        {
            (1, 0, 1.0),
            (2, 1499, 0.9),
            (3, 1500, 0.5),
        };

        var result = GuideTierClassifier.ClassifySet(items);

        result.Should().HaveCount(3);
        result[1].Should().Be(GuideTier.Early);
        result[2].Should().Be(GuideTier.Early);
        result[3].Should().Be(GuideTier.Early);
    }

    [Fact]
    public void ItemsAboveEarlyCutoff_SplitByPickRateMedian()
    {
        var items = new (int ItemId, int TotalCost, double PickRate)[]
        {
            (10, 2000, 0.8),
            (11, 3000, 0.6),
            (12, 4000, 0.4),
            (13, 5000, 0.2),
        };

        var result = GuideTierClassifier.ClassifySet(items);

        result[10].Should().Be(GuideTier.Core);
        result[11].Should().Be(GuideTier.Core);
        result[12].Should().Be(GuideTier.Late);
        result[13].Should().Be(GuideTier.Late);
    }

    [Fact]
    public void SingleItemAboveCutoff_IsCore()
    {
        var items = new (int ItemId, int TotalCost, double PickRate)[] { (42, 4800, 1.0) };

        var result = GuideTierClassifier.ClassifySet(items);

        result[42].Should().Be(GuideTier.Core);
    }

    [Fact]
    public void OddCountAboveCutoff_FavorsCore()
    {
        var items = new (int ItemId, int TotalCost, double PickRate)[]
        {
            (10, 2000, 0.9),
            (11, 2000, 0.5),
            (12, 2000, 0.1),
        };

        var result = GuideTierClassifier.ClassifySet(items);

        result[10].Should().Be(GuideTier.Core);
        result[11].Should().Be(GuideTier.Core);
        result[12].Should().Be(GuideTier.Late);
    }

    [Fact]
    public void TiedPickRateAtBoundary_AllTiesGoToCore()
    {
        var items = new (int ItemId, int TotalCost, double PickRate)[]
        {
            (10, 2000, 0.8),
            (11, 2000, 0.6),
            (12, 2000, 0.6),
            (13, 2000, 0.4),
        };

        var result = GuideTierClassifier.ClassifySet(items);

        result[10].Should().Be(GuideTier.Core);
        result[11].Should().Be(GuideTier.Core);
        result[12].Should().Be(GuideTier.Core);
        result[13].Should().Be(GuideTier.Late);
    }

    [Fact]
    public void AllSamePickRate_AreAllCore()
    {
        var items = new (int ItemId, int TotalCost, double PickRate)[]
        {
            (10, 2000, 0.5),
            (11, 3000, 0.5),
            (12, 4000, 0.5),
        };

        var result = GuideTierClassifier.ClassifySet(items);

        result[10].Should().Be(GuideTier.Core);
        result[11].Should().Be(GuideTier.Core);
        result[12].Should().Be(GuideTier.Core);
    }

    [Fact]
    public void MixedEarlyAndAbove_ClassifiesEachTier()
    {
        var items = new (int ItemId, int TotalCost, double PickRate)[]
        {
            (1, 500, 1.0),
            (2, 1500, 0.9),
            (10, 2000, 0.8),
            (11, 3000, 0.6),
            (12, 4000, 0.4),
            (13, 5000, 0.2),
        };

        var result = GuideTierClassifier.ClassifySet(items);

        result[1].Should().Be(GuideTier.Early);
        result[2].Should().Be(GuideTier.Early);
        result[10].Should().Be(GuideTier.Core);
        result[11].Should().Be(GuideTier.Core);
        result[12].Should().Be(GuideTier.Late);
        result[13].Should().Be(GuideTier.Late);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1499)]
    [InlineData(1500)]
    public void ClassifySingle_AtOrBelowCutoff_ReturnsEarly(int totalCost)
    {
        var tier = GuideTierClassifier.Classify(totalCost);

        tier.Should().Be(GuideTier.Early);
    }

    [Fact]
    public void ClassifySingle_AboveCutoff_ThrowsBecauseContextIsRequired()
    {
        var act = () => GuideTierClassifier.Classify(2000);

        act.Should().Throw<InvalidOperationException>();
    }
}
