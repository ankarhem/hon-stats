using AwesomeAssertions;
using HonStats.App.Search;

namespace HonStats.App.Tests.Search;

public class SearchMatcherScoreTests
{
    [Theory]
    [InlineData("Chaosbrand", "Chaosbrand", 100)] // exact
    [InlineData("phoenixs", "Phoenix's", 100)] // apostrophe-glued exact (normalize equal)
    [InlineData(null, "Behemoth", 100)] // null query = matches-all sentinel
    [InlineData("", "Behemoth", 100)] // blank query = matches-all sentinel
    [InlineData("   ", "Behemoth", 100)] // whitespace query = matches-all sentinel
    [InlineData("chaos", "Chaosbrand", 90)] // prefix
    [InlineData("brand", "Chaosbrand", 80)] // substring / infix
    [InlineData("tlaon", "Phoenix's Talon", 65)] // token-level fuzzy typo
    [InlineData("phoenixstalon", "Phoenix's Talon", 45)] // whole-string typo (dropped space)
    [InlineData("xyz", "Behemoth", 0)] // unrelated
    [InlineData("boot", "Marchers", 0)] // different word
    public void Score_returns_expected_tier(string? query, string name, int expected) =>
        SearchMatcher.Score(query, name).Should().Be(expected);
}
