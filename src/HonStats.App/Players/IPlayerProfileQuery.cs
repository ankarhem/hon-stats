using HonStats.Domain.Players;

namespace HonStats.App.Players;

public interface IPlayerProfileQuery
{
    Task<PlayerProfile?> GetAsync(Guid accountId, CancellationToken ct = default);
}
