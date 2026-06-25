using HonStats.App.Events;
using HonStats.Domain.Events;

namespace HonStats.App.Insights;

// Rebuilds hero-build aggregates for every hero a player has indexed item data
// for, whenever that player's raw match data is refreshed.
public sealed class RebuildHeroBuildsHandler(IInsightsRawQuery raw, IInsightsAggregateStore store)
    : IEventHandler<PlayerMatchesIndexed>
{
    public async Task HandleAsync(PlayerMatchesIndexed @event, CancellationToken ct = default)
    {
        var accountId = @event.AccountId;
        var heroIds = await raw.GetHeroIdsAsync(accountId, ct);

        foreach (var heroId in heroIds)
        {
            var inputs = await raw.GetHeroItemInputsAsync(accountId, heroId, ct);
            var entries = HeroBuildAggregator.Build(inputs);
            await store.ReplaceHeroBuildsAsync(accountId, heroId, entries, ct);
        }
    }
}

// Rebuilds teammate aggregates from the stored match roster.
public sealed class RebuildTeammatesHandler(IInsightsRawQuery raw, IInsightsAggregateStore store)
    : IEventHandler<PlayerMatchesIndexed>
{
    public async Task HandleAsync(PlayerMatchesIndexed @event, CancellationToken ct = default)
    {
        var inputs = await raw.GetTeammateInputsAsync(@event.AccountId, ct);
        var aggregates = TeammateAggregator.Build(inputs);
        await store.ReplaceTeammatesAsync(@event.AccountId, aggregates, ct);
    }
}
