using HonStats.Domain.Matches;

namespace HonStats.App.Matches;

public interface IMatchQuery
{
    Task<IReadOnlyList<PlayerMatch>> GetRecentForPlayerAsync(
        Guid playerId,
        int limit,
        int offset,
        CancellationToken ct = default
    );

    Task<MatchDetail?> GetSummaryAsync(int gameId, CancellationToken ct = default);
}
