using AwesomeAssertions;
using HonStats.App.Search;
using HonStats.Domain.Players;

namespace HonStats.App.Tests.Search;

public class PlayerNameMatcherTests
{
    [Fact]
    public void Exact_query_returns_entry_at_position_zero_with_score_100()
    {
        var matcher = new PlayerNameMatcher(Corpus());

        var results = matcher.Search("hakanjuholt", 10);

        results.Should().NotBeEmpty();
        results[0].Username.Should().Be("hakanjuholt");
        results[0].Score.Should().Be(100);
    }

    [Fact]
    public void Prefix_query_matches_with_score_90()
    {
        var matcher = new PlayerNameMatcher(Corpus());

        var results = matcher.Search("swep", 10);

        results.Should().Contain(r => r.Username == "Sweparn" && r.Score == 90);
    }

    [Fact]
    public void Typo_query_still_finds_target_with_positive_score()
    {
        var matcher = new PlayerNameMatcher(Corpus());

        var results = matcher.Search("hakanjohlt", 10);

        results.Should().Contain(r => r.Username == "hakanjuholt" && r.Score > 0);
    }

    [Fact]
    public void Substring_query_matches_with_score_80()
    {
        var matcher = new PlayerNameMatcher(Corpus());

        var results = matcher.Search("pink", 10);

        results.Should().Contain(r => r.Username == "idealpink" && r.Score == 80);
    }

    [Fact]
    public void Limit_caps_the_number_of_results()
    {
        var matcher = new PlayerNameMatcher(Corpus());

        // "pink" is a prefix of three pink* names and an infix of idealpink -> 4
        // candidates; limit 2 must cap the output to exactly two.
        var results = matcher.Search("pink", 2);

        results.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_or_null_query_returns_empty(string? query)
    {
        var matcher = new PlayerNameMatcher(Corpus());

        matcher.Search(query, 10).Should().BeEmpty();
    }

    [Fact]
    public void Non_positive_limit_returns_empty()
    {
        var matcher = new PlayerNameMatcher(Corpus());

        matcher.Search("pink", 0).Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_account_id_is_deduped_keeping_first_occurrence()
    {
        var dupId = Guid.NewGuid();
        var corpus = new List<PlayerCorpusEntry>
        {
            new() { AccountId = dupId, Username = "dupone" },
            new() { AccountId = dupId, Username = "duptwo" }, // duplicate id
            new() { AccountId = Guid.NewGuid(), Username = "solo" },
        };

        var matcher = new PlayerNameMatcher(corpus);

        matcher.Count.Should().Be(2);
        var results = matcher.Search("dup", 10);
        results.Should().HaveCount(1);
        results[0].Username.Should().Be("dupone"); // first occurrence wins
    }

    [Fact]
    public void Entries_without_a_usable_name_are_skipped()
    {
        var corpus = new List<PlayerCorpusEntry>
        {
            new() { AccountId = Guid.NewGuid() }, // both names null -> skipped
            new()
            {
                AccountId = Guid.NewGuid(),
                Username = "   ",
                DisplayName = null,
            }, // blank -> skipped
            new() { AccountId = Guid.NewGuid(), Username = "named" },
        };

        var matcher = new PlayerNameMatcher(corpus);

        matcher.Count.Should().Be(1);
    }

    [Fact]
    public void DisplayName_is_used_as_searchable_name_when_username_is_empty()
    {
        var corpus = new List<PlayerCorpusEntry>
        {
            new() { AccountId = Guid.NewGuid(), DisplayName = "DisplayOnly" },
        };

        var matcher = new PlayerNameMatcher(corpus);

        var results = matcher.Search("display", 10);
        results.Should().HaveCount(1);
        results[0].Username.Should().Be("DisplayOnly");
        results[0].DisplayName.Should().Be("DisplayOnly");
    }

    [Fact]
    public void Results_are_ordered_by_score_descending()
    {
        var matcher = new PlayerNameMatcher(Corpus());

        // "pink": three pink* names score 90 (prefix), idealpink scores 80 (infix).
        // A 90-score entry must precede the 80-score entry.
        var results = matcher.Search("pink", 10);
        var indexed = results.Select((r, i) => (Row: r, Index: i)).ToList();

        var idx90 = indexed.First(x => x.Row.Score == 90).Index;
        var idx80 = indexed.First(x => x.Row.Score == 80).Index;

        idx90.Should().BeLessThan(idx80);
    }

    [Fact]
    public void Count_equals_number_of_usable_name_entries()
    {
        var corpus = Corpus();

        var matcher = new PlayerNameMatcher(corpus);

        matcher.Count.Should().Be(corpus.Count);
    }

    private static List<PlayerCorpusEntry> Corpus() =>
        new()
        {
            Entry("hakanjuholt"),
            Entry("ChadsRandom"),
            Entry("Pumbalicious"),
            Entry("idealpink"),
            Entry("Sweparn"),
            Entry("VirginsPick"),
            Entry("Izcode"),
            Entry("jimmyakesson"),
            // pink* cluster: exercises multi-candidate ordering and limit capping.
            Entry("pinkfloyd"),
            Entry("pinkpanther"),
            Entry("pinkelephant"),
        };

    private static PlayerCorpusEntry Entry(string name) =>
        new() { AccountId = Guid.NewGuid(), Username = name };
}
