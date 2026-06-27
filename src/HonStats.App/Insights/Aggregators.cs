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

public static class MapStatsAggregator
{
    public static IReadOnlyList<MapStatEntry> Build(IReadOnlyList<MatchStatInput> inputs)
    {
        return inputs
            .GroupBy(i => i.Map)
            .Select(g => BuildEntry(g.ToList()))
            .OrderByDescending(e => e.Games)
            .ThenBy(e => e.Map, StringComparer.Ordinal)
            .ToList();
    }

    private static MapStatEntry BuildEntry(IReadOnlyList<MatchStatInput> matches)
    {
        var games = matches.Count;
        var wins = matches.Count(m => m.Won);

        // GPM is an aggregate rate over the gold-bearing games only: total gold
        // earned divided by total minutes played. Null when no game carries gold.
        var goldGames = matches.Where(m => m.GoldEarned.HasValue && m.DurationSeconds > 0).ToList();
        var totalMinutes = goldGames.Sum(m => m.DurationSeconds / 60.0);
        double? avgGpm =
            goldGames.Count > 0 && totalMinutes > 0
                ? goldGames.Sum(m => m.GoldEarned!.Value) / totalMinutes
                : null;

        return new MapStatEntry
        {
            Map = matches[0].Map,
            Games = games,
            AvgKills = games > 0 ? matches.Sum(m => m.Kills) / (double)games : 0,
            AvgDeaths = games > 0 ? matches.Sum(m => m.Deaths) / (double)games : 0,
            AvgAssists = games > 0 ? matches.Sum(m => m.Assists) / (double)games : 0,
            AvgGPM = avgGpm,
            Wins = wins,
            WinRate = games > 0 ? wins * 100.0 / games : 0,
        };
    }
}
