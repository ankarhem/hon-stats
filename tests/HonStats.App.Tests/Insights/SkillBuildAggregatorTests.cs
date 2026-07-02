using AwesomeAssertions;
using HonStats.App.Insights;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using Xunit;

namespace HonStats.App.Tests.Insights;

public class SkillBuildAggregatorTests
{
    private static readonly Guid LegionA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid LegionB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid HellA = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    [Fact]
    public void EmitsLevelUps_InTimeOrder()
    {
        var replay = Replay(
            gameId: 100,
            anchor: Anchor(AnchorTeam(LegionA)),
            snapshots:
            [
                Snap(-90, Pos()),
                Snap(30, Pos(Skill(1017, 1))),
                Snap(60, Pos(Skill(1017, 2))),
                Snap(300, Pos(Skill(1018, 1), Skill(1017, 3))),
            ]
        );

        var result = SkillBuildAggregator.Build(replay);

        result
            .Select(e => (e.SkillId, e.Level, e.TimeSeconds))
            .Should()
            .Equal((1017, 1, 30), (1017, 2, 60), (1018, 1, 300), (1017, 3, 300));
        result.Should().OnlyContain(e => e.GameId == 100 && e.AccountId == LegionA);
    }

    [Fact]
    public void MapsPositionToAccountId_ViaSnapshotZeroAnchor()
    {
        var replay = Replay(
            gameId: 1,
            anchor: Anchor(AnchorTeam(LegionA, LegionB), AnchorTeam(HellA)),
            Snap(30, Pos(Skill(11, 1)), Pos(Skill(22, 1)))
        );

        var result = SkillBuildAggregator.Build(replay);

        result
            .Should()
            .ContainEquivalentOf(
                new SkillLevelEvent
                {
                    GameId = 1,
                    AccountId = LegionA,
                    SkillId = 11,
                    Level = 1,
                    TimeSeconds = 30,
                }
            );
        result
            .Should()
            .ContainEquivalentOf(
                new SkillLevelEvent
                {
                    GameId = 1,
                    AccountId = HellA,
                    SkillId = 22,
                    Level = 1,
                    TimeSeconds = 30,
                }
            );
        result.Should().NotContain(e => e.AccountId == LegionB);
    }

    [Fact]
    public void EmptyReplay_ReturnsEmpty()
    {
        var replay = new ParsedReplay { GameId = 1 };

        SkillBuildAggregator.Build(replay).Should().BeEmpty();
    }

    [Fact]
    public void AnchorWithoutAccountIds_ReturnsEmpty()
    {
        var replay = new ParsedReplay
        {
            GameId = 1,
            Snapshots =
            [
                new()
                {
                    Time = 0,
                    Teams = [new() { Players = [new() { Skills = [Skill(1017, 1)] }] }],
                },
            ],
        };

        SkillBuildAggregator.Build(replay).Should().BeEmpty();
    }

    [Fact]
    public void DegenerateAnchorOnlyReplay_ReturnsEmptyBuild()
    {
        var replay = new ParsedReplay { GameId = 1, Snapshots = [Anchor(AnchorTeam(LegionA))] };

        SkillBuildAggregator.IsDegenerate(replay).Should().BeTrue();
        SkillBuildAggregator.Build(replay).Should().BeEmpty();
    }

    private static ParsedReplay Replay(
        int gameId,
        ReplaySnapshot anchor,
        params ReplaySnapshot[] snapshots
    ) => new() { GameId = gameId, Snapshots = [anchor, .. snapshots] };

    private static ReplaySnapshot Anchor(params ReplayTeam[] teams) =>
        new() { Time = -90, Teams = teams.ToList() };

    private static ReplayTeam AnchorTeam(params Guid[] accounts) =>
        new() { Players = accounts.Select(a => new ReplayPlayer { AccountId = a }).ToList() };

    private static ReplaySnapshot Snap(int time, params ReplayTeam[] teams) =>
        new() { Time = time, Teams = teams.ToList() };

    private static ReplayTeam Pos(params ReplaySkill[] skills) =>
        new() { Players = [new() { Skills = skills.ToList() }] };

    private static ReplaySkill Skill(int skillId, int level) => new(skillId, level);
}
