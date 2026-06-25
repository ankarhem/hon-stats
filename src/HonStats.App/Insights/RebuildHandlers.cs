using HonStats.App.Events;
using HonStats.App.Players;
using HonStats.Domain.Events;
using HonStats.Domain.Insights;

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
public sealed class RebuildTeammatesHandler(
    IInsightsRawQuery raw,
    IInsightsAggregateStore store,
    IPlayerNameResolver names
) : IEventHandler<PlayerMatchesIndexed>
{
    public async Task HandleAsync(PlayerMatchesIndexed @event, CancellationToken ct = default)
    {
        var accountId = @event.AccountId;
        var inputs = await raw.GetTeammateInputsAsync(accountId, ct);
        var aggregates = TeammateAggregator.Build(inputs);

        var ids = aggregates.Select(a => a.TeammateAccountId).ToList();
        var resolved = await names.ResolveAsync(ids, ct);

        var rows = aggregates
            .Select(a =>
            {
                resolved.TryGetValue(a.TeammateAccountId, out var name);
                return new Teammate
                {
                    AccountId = accountId,
                    TeammateAccountId = a.TeammateAccountId,
                    GamesTogether = a.GamesTogether,
                    WinsTogether = a.WinsTogether,
                    DisplayName = name?.DisplayName,
                    Username = name?.Username,
                    Country = name?.Country,
                };
            })
            .ToList();

        await store.ReplaceTeammatesAsync(accountId, rows, ct);
    }
}
