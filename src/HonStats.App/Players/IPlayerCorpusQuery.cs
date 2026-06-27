using HonStats.Domain.Players;

namespace HonStats.App.Players;

public interface IPlayerCorpusQuery
{
    Task<IReadOnlyList<PlayerCorpusEntry>> GetNamedPlayersAsync(CancellationToken ct = default);
}
