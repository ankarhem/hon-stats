using HonStats.Domain.Matches;

namespace HonStats.App.Matches;

public interface IParsedReplayQuery
{
    Task<ParsedReplay?> GetAsync(int gameId, CancellationToken ct = default);
}
