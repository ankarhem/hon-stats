using AwesomeAssertions;
using HonStats.App.Search;
using Xunit;

namespace HonStats.App.Tests.Search;

public class SearchMatcherTests
{
    [Theory]
    [InlineData("phoenix", "Phoenix's Talon")] // apostrophe prefix
    [InlineData("phoenixs", "Phoenix's Talon")] // apostrophe omitted
    [InlineData("PHOENIX", "Phoenix's Talon")] // case-insensitive
    [InlineData("phoenix talon", "Phoenix's Talon")] // multi-token, no apostrophe
    [InlineData("talon", "Phoenix's Talon")] // suffix token
    [InlineData("tlaon", "Phoenix's Talon")] // typo in suffix token
    [InlineData("chaos", "Chaosbrand")] // prefix substring
    [InlineData("brand", "Chaosbrand")] // infix substring
    [InlineData("chaosbrnad", "Chaosbrand")] // transposition (whole-string fuzzy)
    [InlineData("behomoth", "Behemoth")] // single substitution
    [InlineData("", "Behemoth")] // blank query matches all
    [InlineData(null, "Behemoth")] // null query matches all
    public void Matches_accepts_typos_and_apostrophes(string? query, string name) =>
        SearchMatcher.Matches(query, name).Should().BeTrue();

    [Theory]
    [InlineData("xyz", "Behemoth")] // unrelated
    [InlineData("boot", "Marchers")] // different word
    [InlineData("phoenix", "Chaosbrand")] // not a substring/token, far edit distance
    public void Rejects_unrelated(string? query, string name) =>
        SearchMatcher.Matches(query, name).Should().BeFalse();

    [Theory]
    [InlineData("Phoenix's Talon", "phoenixs talon")] // apostrophe glued
    [InlineData("Chaosbrand", "chaosbrand")]
    [InlineData("Café", "cafe")] // diacritic folded
    [InlineData("Keeper of the Forest", "keeper of the forest")]
    public void Normalize_collapses_to_search_key(string input, string expected) =>
        SearchMatcher.Normalize(input).Should().Be(expected);
}
