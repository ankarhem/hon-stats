namespace HonStats.App.Leaderboard;

public interface ILeaderboardQuery
{
    Task<IReadOnlyList<LeaderboardEntry>> GetAsync(string map, CancellationToken ct = default);
}

public sealed record LeaderboardEntry(
    int Rank,
    Guid AccountId,
    string DisplayName,
    string Username,
    double Mmr,
    string? Country,
    string RankName,
    string RankIcon,
    int StarLevel,
    string? Color,
    string? Avatar
);
