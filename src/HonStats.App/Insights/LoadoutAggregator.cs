using HonStats.Domain.Insights;

namespace HonStats.App.Insights;

// Pure aggregation logic. No I/O, no DI — fully unit-testable.
//
// A "loadout" is the set of non-consumable items in a player's final inventory
// for a single match. This aggregator reduces a sequence of per-match inventories
// into loadout -> count + win rate, so a player can see which full builds recur
// and how they perform. Consumable ids are supplied by the caller (resolved from
// gamedata reference data upstream); the aggregator treats them as an opaque set.
public static class LoadoutAggregator
{
    // Cap keeps the UI compact: loadouts are dense (one icon per item), so only
    // the most frequent builds are worth surfacing.
    private const int MaxLoadouts = 5;

    public static IReadOnlyList<LoadoutEntry> Build(
        IReadOnlyList<MatchItemInput> inputs,
        ISet<int> consumableItemIds
    )
    {
        // Reduce each match to its loadout: non-consumable items, deduped and
        // sorted ascending so slot order doesn't affect the key. Matches whose
        // inventory was entirely consumables yield an empty loadout and are dropped.
        var perMatch = inputs
            .Select(input => new
            {
                Input = input,
                Loadout = input
                    .ItemIds.Where(id => !consumableItemIds.Contains(id))
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList(),
            })
            .Where(x => x.Loadout.Count > 0)
            .ToList();

        if (perMatch.Count == 0)
            return [];

        // Group identical loadouts (by their canonical sorted-item string) and
        // project each group to a LoadoutEntry. Every member of a group shares
        // the same item set, so the first member's loadout is the representative.
        return perMatch
            .GroupBy(x => string.Join(",", x.Loadout), StringComparer.Ordinal)
            .Select(group =>
            {
                var count = group.Count();
                var wins = group.Count(x => x.Input.Won);
                return new LoadoutEntry(group.First().Loadout, count, wins, (double)wins / count);
            })
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.ItemIds[0])
            .Take(MaxLoadouts)
            .ToList();
    }
}
