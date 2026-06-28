using HonStats.Domain.Insights;

namespace HonStats.App.Insights;

public interface IPlayerInsightsQuery
{
    Task<IndexedPlayer?> GetIndexedPlayerAsync(Guid accountId, CancellationToken ct = default);

    Task<IReadOnlyList<TeammateStat>> GetTeammatesAsync(
        Guid accountId,
        int limit,
        int offset,
        string? map = null,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<HeroBuildEntry>> GetHeroBuildAsync(
        Guid accountId,
        int heroId,
        string? map = null,
        CancellationToken ct = default
    );

    // Mean first-buy second per item for a player+hero. Only items with replay-timing
    // data appear (match_item_timing rows exist only when replay data is available).
    Task<IReadOnlyList<ItemTimingEntry>> GetHeroItemTimingAsync(
        Guid accountId,
        int heroId,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<int, int>> GetHeroGamesAsync(
        Guid accountId,
        string? map = null,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<MapStatEntry>> GetMapStatsAsync(
        Guid accountId,
        CancellationToken ct = default
    );

    // Aggregates all maps into a single "all" entry (Map="all"). Replaces the
    // juvio getprofilestats dependency for the stats panel overall view.
    Task<MapStatEntry> GetOverallStatsAsync(Guid accountId, CancellationToken ct = default);
}

public interface IPlayerInsightsIndexer
{
    Task IndexAsync(Guid accountId, bool forceBackfill = false, CancellationToken ct = default);
}
