namespace HonStats.App.Leaderboard;

public interface IMatchCountQuery
{
    Task<MatchCounts?> GetAsync(CancellationToken ct = default);
}

public sealed record MatchCounts(int TotalMatches, int FocMatches, int MidwarsMatches);
