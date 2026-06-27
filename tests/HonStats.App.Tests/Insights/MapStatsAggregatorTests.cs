using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class MapStatsAggregatorTests
{
    [Fact]
    public void EmptyInputs_ReturnsEmpty()
    {
        var result = MapStatsAggregator.Build([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GroupsByMap_ComputesAvgKdaAndWinRate()
    {
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                Kills = 10,
                Deaths = 5,
                Assists = 15,
                DurationSeconds = 1800,
                Won = true,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                Kills = 4,
                Deaths = 6,
                Assists = 8,
                DurationSeconds = 1800,
                Won = false,
            },
            new()
            {
                GameId = 3,
                Map = "MidWars",
                Kills = 20,
                Deaths = 3,
                Assists = 10,
                DurationSeconds = 900,
                Won = true,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        result.Should().HaveCount(2);

        var caldavar = result.Single(e => e.Map == "ForestsOfCaldavar");
        caldavar.Games.Should().Be(2);
        caldavar.AvgKills.Should().BeApproximately(7.0, 0.0001);
        caldavar.AvgDeaths.Should().BeApproximately(5.5, 0.0001);
        caldavar.AvgAssists.Should().BeApproximately(11.5, 0.0001);
        caldavar.Wins.Should().Be(1);
        caldavar.WinRate.Should().BeApproximately(0.5, 0.0001);

        var mid = result.Single(e => e.Map == "MidWars");
        mid.Games.Should().Be(1);
        mid.AvgKills.Should().BeApproximately(20.0, 0.0001);
        mid.AvgDeaths.Should().BeApproximately(3.0, 0.0001);
        mid.AvgAssists.Should().BeApproximately(10.0, 0.0001);
        mid.Wins.Should().Be(1);
        mid.WinRate.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void AvgWards_SummedAcrossGamesDividedByGameCount()
    {
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                WardsPlaced = 10,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                WardsPlaced = 4,
            },
            new()
            {
                GameId = 3,
                Map = "MidWars",
                WardsPlaced = 0,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var caldavar = result.Single(e => e.Map == "ForestsOfCaldavar");
        // (10 + 4) / 2 games = 7.
        caldavar.AvgWards.Should().BeApproximately(7.0, 0.0001);

        var mid = result.Single(e => e.Map == "MidWars");
        // 0 / 1 = 0.
        mid.AvgWards.Should().BeApproximately(0.0, 0.0001);
    }

    [Fact]
    public void GpmComputed_OnlyOverGamesWithGold_AndAggregateAcrossThem()
    {
        // game1 + game3 carry gold; game2 has no gold breakdown (pre-gold match).
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                GoldEarned = 600,
                DurationSeconds = 600,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                GoldEarned = null,
                DurationSeconds = 600,
            },
            new()
            {
                GameId = 3,
                Map = "ForestsOfCaldavar",
                GoldEarned = 600,
                DurationSeconds = 1200,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var caldavar = result.Single(e => e.Map == "ForestsOfCaldavar");
        caldavar.Games.Should().Be(3);
        // aggregate: total gold 1200 / total minutes (10 + 20) = 1200 / 30 = 40.
        caldavar.AvgGPM.Should().BeApproximately(40.0, 0.0001);
    }

    [Fact]
    public void AllNullGold_YieldsNullAvgGpm()
    {
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "MidWars",
                GoldEarned = null,
                DurationSeconds = 900,
            },
            new()
            {
                GameId = 2,
                Map = "MidWars",
                GoldEarned = null,
                DurationSeconds = 900,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var mid = result.Single(e => e.Map == "MidWars");
        mid.AvgGPM.Should().BeNull();
    }

    [Fact]
    public void ZeroDurationGames_ExcludedFromGpmAndDoNotDivideByZero()
    {
        // game1 has gold but duration 0 -> excluded. game2 is the only contributor.
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                GoldEarned = 600,
                DurationSeconds = 0,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                GoldEarned = 600,
                DurationSeconds = 600,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var caldavar = result.Single(e => e.Map == "ForestsOfCaldavar");
        // only game2: 600 gold / 10 minutes = 60.
        caldavar.AvgGPM.Should().BeApproximately(60.0, 0.0001);
    }

    [Fact]
    public void OrdersByGamesDescendingThenMapAscending()
    {
        var inputs = new List<MatchStatInput>
        {
            new() { GameId = 1, Map = "Zeta" },
            new() { GameId = 2, Map = "Alpha" },
            new() { GameId = 3, Map = "Alpha" },
            new() { GameId = 4, Map = "Alpha" },
            new() { GameId = 5, Map = "Beta" },
            new() { GameId = 6, Map = "Beta" },
            new() { GameId = 7, Map = "Beta" },
        };

        var result = MapStatsAggregator.Build(inputs);

        // Alpha and Beta both have 3 games; Zeta has 1.
        // Tie on Games (3) broken by Map ascending: Alpha before Beta; Zeta last.
        result.Select(e => e.Map).Should().Equal(["Alpha", "Beta", "Zeta"]);
    }

    [Fact]
    public void Build_ComputesAvgXPM_FromExperience()
    {
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                Experience = 600,
                DurationSeconds = 600,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                Experience = 1200,
                DurationSeconds = 600,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var caldavar = result.Single(e => e.Map == "ForestsOfCaldavar");
        // (600 + 1200) xp / (10 + 10) minutes = 1800 / 20 = 90.
        caldavar.AvgXPM.Should().BeApproximately(90.0, 0.0001);
    }

    [Fact]
    public void Build_NullExperience_SkippedFromXPMDenominator()
    {
        // game1 carries xp; game2 has no xp breakdown. Only game1 contributes to
        // numerator AND denominator (separate-denominator pattern, like GPM).
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                Experience = 600,
                DurationSeconds = 600,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                Experience = null,
                DurationSeconds = 600,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var caldavar = result.Single(e => e.Map == "ForestsOfCaldavar");
        caldavar.Games.Should().Be(2);
        // 600 / 10 minutes = 60 — NOT 600 / 20 (the null-xp game's minutes excluded).
        caldavar.AvgXPM.Should().BeApproximately(60.0, 0.0001);
    }

    [Fact]
    public void Build_ComputesAvgDPM_FromHeroDamage()
    {
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                HeroDamage = 30000,
                DurationSeconds = 600,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                HeroDamage = 60000,
                DurationSeconds = 1200,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var caldavar = result.Single(e => e.Map == "ForestsOfCaldavar");
        // (30000 + 60000) damage / (10 + 20) minutes = 90000 / 30 = 3000.
        caldavar.AvgDPM.Should().BeApproximately(3000.0, 0.0001);
    }

    [Fact]
    public void Build_AllExperienceNull_AvgXPMIsNull()
    {
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "MidWars",
                Experience = null,
                DurationSeconds = 900,
            },
            new()
            {
                GameId = 2,
                Map = "MidWars",
                Experience = null,
                DurationSeconds = 900,
            },
        };

        var result = MapStatsAggregator.Build(inputs);

        var mid = result.Single(e => e.Map == "MidWars");
        mid.AvgXPM.Should().BeNull();
    }

    [Fact]
    public void BuildOverall_AggregatesAllMapsIntoSingleEntry()
    {
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                Kills = 10,
                Deaths = 5,
                Assists = 15,
                DurationSeconds = 1800,
                Won = true,
            },
            new()
            {
                GameId = 2,
                Map = "ForestsOfCaldavar",
                Kills = 4,
                Deaths = 6,
                Assists = 8,
                DurationSeconds = 1800,
                Won = false,
            },
            new()
            {
                GameId = 3,
                Map = "MidWars",
                Kills = 20,
                Deaths = 3,
                Assists = 10,
                DurationSeconds = 900,
                Won = true,
            },
        };

        var overall = MapStatsAggregator.BuildOverall(inputs);

        overall.Map.Should().Be("all");
        overall.Games.Should().Be(3);
        // (10 + 4 + 20) / 3, (5 + 6 + 3) / 3, (15 + 8 + 10) / 3.
        overall.AvgKills.Should().BeApproximately(34.0 / 3.0, 0.0001);
        overall.AvgDeaths.Should().BeApproximately(14.0 / 3.0, 0.0001);
        overall.AvgAssists.Should().BeApproximately(33.0 / 3.0, 0.0001);
        overall.Wins.Should().Be(2);
        overall.WinRate.Should().BeApproximately(2.0 / 3.0, 0.0001);
    }

    [Fact]
    public void BuildOverall_ComputesXpmAndDpm()
    {
        // Inputs span two maps; BuildOverall aggregates raw totals across all of
        // them (no per-map grouping). Null-xp game is excluded from the xp
        // denominator, mirroring per-map Build.
        var inputs = new List<MatchStatInput>
        {
            new()
            {
                GameId = 1,
                Map = "ForestsOfCaldavar",
                Experience = 600,
                HeroDamage = 15000,
                DurationSeconds = 600,
            },
            new()
            {
                GameId = 2,
                Map = "MidWars",
                Experience = null,
                HeroDamage = 30000,
                DurationSeconds = 600,
            },
        };

        var overall = MapStatsAggregator.BuildOverall(inputs);

        // XPM: only game1 carries xp -> 600 / 10 = 60 (game2's minutes excluded).
        overall.AvgXPM.Should().BeApproximately(60.0, 0.0001);
        // DPM: both carry damage -> (15000 + 30000) / (10 + 10) = 45000 / 20 = 2250.
        overall.AvgDPM.Should().BeApproximately(2250.0, 0.0001);
    }
}
