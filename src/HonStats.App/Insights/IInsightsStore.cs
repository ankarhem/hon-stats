using HonStats.Domain.Insights;

namespace HonStats.App.Insights;

// Read side of the raw store for aggregate rebuilders.
public interface IInsightsRawQuery
{
    Task<IReadOnlyList<int>> GetHeroIdsAsync(Guid accountId, CancellationToken ct = default);

    Task<IReadOnlyList<MatchItemInput>> GetHeroItemInputsAsync(
        Guid accountId,
        int heroId,
        string? map = null,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<MatchTeammateInput>> GetTeammateInputsAsync(
        Guid accountId,
        string? map = null,
        CancellationToken ct = default
    );
}

// Write side for rebuilt aggregates.
public interface IInsightsAggregateStore
{
    Task ReplaceHeroBuildsAsync(
        Guid accountId,
        int heroId,
        IReadOnlyList<HeroBuildEntry> entries,
        CancellationToken ct = default
    );

    Task ReplaceTeammatesAsync(
        Guid accountId,
        IReadOnlyList<Teammate> teammates,
        CancellationToken ct = default
    );
}
