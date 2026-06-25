using HonStats.Domain.Players;

namespace HonStats.App.Players;

public interface IPlayerSearch
{
    Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(
        string username,
        CancellationToken ct = default
    );
}
