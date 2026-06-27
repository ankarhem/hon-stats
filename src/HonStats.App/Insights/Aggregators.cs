using HonStats.Domain.Insights;
using HonStats.Domain.Matches;

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

// Derives per-(game, player, item) first-seen-second from a parsed replay's snapshot
// timeline. Pure: no I/O, no DI.
//
// Position->player mapping: replay snapshot players are positional (team-index +
// player-index) and carry NO identity on non-anchor snapshots. But snapshot[0]'s
// players DO carry AccountId + HeroId (verified empirically for gameId 8008220:
// mapping each snapshot[0] position->AccountId reproduces the summary's HeroId for
// that AccountId 10/10, and NetWorth matches within the end-game gold tick). So the
// replay self-anchors — no external summary is needed for the mapping. The position
// grid is stable across the timeline (every snapshot keeps the same team/player
// count), so snapshot[0]'s (teamIdx, playerIdx)->AccountId map applies throughout.
public static class ItemTimingAggregator
{
    // 0xFFFF is juvio's empty-slot sentinel on the replay timeline; 0 is unused.
    private static readonly HashSet<int> SentinelItemIds = [0, 65535];

    public static IReadOnlyList<ItemBuyTime> Build(ParsedReplay replay)
    {
        if (replay.Snapshots.Count == 0)
            return [];

        var anchor = replay.Snapshots[0];
        var positionToAccount = new Dictionary<(int Team, int Player), Guid>();
        for (var ti = 0; ti < anchor.Teams.Count; ti++)
        {
            var players = anchor.Teams[ti].Players;
            for (var pi = 0; pi < players.Count; pi++)
            {
                if (players[pi].AccountId is { } id && id != Guid.Empty)
                    positionToAccount[(ti, pi)] = id;
            }
        }

        if (positionToAccount.Count == 0)
            return [];

        var firstSeen = new Dictionary<(Guid Account, int Item), int>();
        foreach (
            var snapshot in replay
                .Snapshots.OrderBy(s => s.Time)
                .ThenBy(s => replay.Snapshots.IndexOf(s))
        )
        {
            for (var ti = 0; ti < snapshot.Teams.Count; ti++)
            {
                var players = snapshot.Teams[ti].Players;
                for (var pi = 0; pi < players.Count; pi++)
                {
                    if (!positionToAccount.TryGetValue((ti, pi), out var account))
                        continue;

                    foreach (
                        var item in players[pi]
                            .Items.Select(i => i.ItemId)
                            .Where(id => !SentinelItemIds.Contains(id))
                    )
                    {
                        if (!firstSeen.ContainsKey((account, item)))
                            firstSeen[(account, item)] = snapshot.Time;
                    }
                }
            }
        }

        return firstSeen
            .Select(kv => new ItemBuyTime
            {
                GameId = replay.GameId,
                AccountId = kv.Key.Account,
                ItemId = kv.Key.Item,
                FirstSeenSeconds = kv.Value,
            })
            .OrderBy(t => t.AccountId)
            .ThenBy(t => t.ItemId)
            .ToList();
    }
}
