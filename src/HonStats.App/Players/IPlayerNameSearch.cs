using HonStats.Domain.Players;

namespace HonStats.App.Players;

public interface IPlayerNameSearch
{
    Task<IReadOnlyList<PlayerNameSearchResult>> SearchAsync(
        string query,
        int limit,
        CancellationToken ct = default
    );
}
