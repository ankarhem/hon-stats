using HonStats.Domain.Insights;

namespace HonStats.App.Insights;

// Pure aggregation logic. No I/O, no DI — fully unit-testable. Driven by the
// rebuild handlers from raw-store inputs.
public static class HeroBuildAggregator
{
    public static IReadOnlyList<HeroBuildEntry> Build(IReadOnlyList<MatchItemInput> inputs)
    {
        if (inputs.Count == 0)
            return [];

        var frequency = new Dictionary<int, int>();
        var wins = new Dictionary<int, int>();
        foreach (var input in inputs)
        {
            foreach (var itemId in input.ItemIds.Distinct())
            {
                frequency[itemId] = frequency.TryGetValue(itemId, out var count) ? count + 1 : 1;
                if (input.Won)
                    wins[itemId] = wins.TryGetValue(itemId, out var w) ? w + 1 : 1;
            }
        }

        return frequency
            .Select(kv => new HeroBuildEntry
            {
                ItemId = kv.Key,
                Frequency = kv.Value,
                Wins = wins.TryGetValue(kv.Key, out var w) ? w : 0,
                Games = inputs.Count,
            })
            .OrderByDescending(entry => entry.Frequency)
            .ThenBy(entry => entry.ItemId)
            .ToList();
    }
}

public static class TeammateAggregator
{
    public static IReadOnlyList<TeammateAggregate> Build(IReadOnlyList<MatchTeammateInput> inputs)
    {
        var games = new Dictionary<Guid, int>();
        var wins = new Dictionary<Guid, int>();
        foreach (var input in inputs)
        {
            foreach (var teammateId in input.TeammateAccountIds.Distinct())
            {
                games[teammateId] = games.TryGetValue(teammateId, out var g) ? g + 1 : 1;
                if (input.Won)
                    wins[teammateId] = wins.TryGetValue(teammateId, out var w) ? w + 1 : 1;
            }
        }

        return games
            .Select(kv => new TeammateAggregate
            {
                TeammateAccountId = kv.Key,
                GamesTogether = kv.Value,
                WinsTogether = wins.TryGetValue(kv.Key, out var w) ? w : 0,
            })
            .OrderByDescending(t => t.GamesTogether)
            .ThenBy(t => t.TeammateAccountId)
            .ToList();
    }
}

public static class HeroItemPairAggregator
{
    public static IReadOnlyList<HeroItemPairEntry> Build(IReadOnlyList<MatchItemInput> inputs)
    {
        if (inputs.Count == 0)
            return [];

        var frequency = new Dictionary<(int ItemA, int ItemB), int>();
        foreach (var input in inputs)
        {
            var distinct = input.ItemIds.Distinct().OrderBy(id => id).ToList();
            for (var i = 0; i < distinct.Count; i++)
            {
                for (var j = i + 1; j < distinct.Count; j++)
                {
                    var pair = (distinct[i], distinct[j]);
                    frequency[pair] = frequency.TryGetValue(pair, out var count) ? count + 1 : 1;
                }
            }
        }

        return frequency
            .Select(kv => new HeroItemPairEntry
            {
                ItemA = kv.Key.ItemA,
                ItemB = kv.Key.ItemB,
                Frequency = kv.Value,
                Games = inputs.Count,
            })
            .OrderByDescending(entry => entry.Frequency)
            .ThenBy(entry => entry.ItemA)
            .ThenBy(entry => entry.ItemB)
            .ToList();
    }
}
