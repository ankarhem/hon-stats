using AwesomeAssertions;
using HonStats.App.Insights;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class GuideTierClassifierTests
{
    private static readonly (int ItemId, double AvgSeconds)[] EmptyTime = [];

    [Fact]
    public void ClassifyByTime_EmptyInput_ReturnsEmptyDictionary()
    {
        var result = GuideTierClassifier.ClassifyByTime(
            EmptyTime,
            earlyCutoffMinutes: 6,
            lateCutoffMinutes: 20
        );

        result.Should().BeEmpty();
    }

    [Fact]
    public void ClassifyByTime_ItemInEarlyTier_WhenAvgSecondsBelowEarlyCutoff()
    {
        var items = new (int ItemId, double AvgSeconds)[] { (1, 120) };

        var result = GuideTierClassifier.ClassifyByTime(
            items,
            earlyCutoffMinutes: 6,
            lateCutoffMinutes: 20
        );

        result[1].Should().Be(GuideTier.Early);
    }

    [Fact]
    public void ClassifyByTime_ItemInCoreTier_WhenAvgSecondsBetweenCutoffs()
    {
        var items = new (int ItemId, double AvgSeconds)[] { (2, 600) };

        var result = GuideTierClassifier.ClassifyByTime(
            items,
            earlyCutoffMinutes: 6,
            lateCutoffMinutes: 20
        );

        result[2].Should().Be(GuideTier.Core);
    }

    [Fact]
    public void ClassifyByTime_ItemInLateTier_WhenAvgSecondsAtOrAboveLateCutoff()
    {
        var items = new (int ItemId, double AvgSeconds)[] { (3, 1500), (4, 1200) };

        var result = GuideTierClassifier.ClassifyByTime(
            items,
            earlyCutoffMinutes: 6,
            lateCutoffMinutes: 20
        );

        result[3].Should().Be(GuideTier.Late);
        result[4].Should().Be(GuideTier.Late);
    }

    [Fact]
    public void ClassifyByTime_ItemAtEarlyBoundary_IsCore()
    {
        var items = new (int ItemId, double AvgSeconds)[] { (5, 360) };

        var result = GuideTierClassifier.ClassifyByTime(
            items,
            earlyCutoffMinutes: 6,
            lateCutoffMinutes: 20
        );

        result[5].Should().Be(GuideTier.Core);
    }

    [Fact]
    public void ClassifyByTime_ZeroOrMissingSeconds_DefaultsToEarly()
    {
        var items = new (int ItemId, double AvgSeconds)[] { (6, 0) };

        var result = GuideTierClassifier.ClassifyByTime(
            items,
            earlyCutoffMinutes: 6,
            lateCutoffMinutes: 20
        );

        result[6].Should().Be(GuideTier.Early);
    }
}
