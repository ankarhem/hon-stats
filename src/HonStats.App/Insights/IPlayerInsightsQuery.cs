using HonStats.Domain.Insights;

namespace HonStats.App.Insights;

public interface IPlayerInsightsQuery
{
    Task<IndexedPlayer?> GetIndexedPlayerAsync(Guid accountId, CancellationToken ct = default);

    Task<IReadOnlyList<TeammateStat>> GetTeammatesAsync(
        Guid accountId,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<HeroBuildEntry>> GetHeroBuildAsync(
        Guid accountId,
        int heroId,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<int, int>> GetHeroGamesAsync(
        Guid accountId,
        CancellationToken ct = default
    );
}

public interface IPlayerInsightsIndexer
{
    Task IndexAsync(Guid accountId, CancellationToken ct = default);
}
