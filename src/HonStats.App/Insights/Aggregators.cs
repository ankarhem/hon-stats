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

public static class MapStatsAggregator
{
    public const string OverallMap = "all";

    public static IReadOnlyList<MapStatEntry> Build(IReadOnlyList<MatchStatInput> inputs)
    {
        return inputs
            .GroupBy(i => i.Map)
            .Select(g => BuildEntry(g.ToList()))
            .OrderByDescending(e => e.Games)
            .ThenBy(e => e.Map, StringComparer.Ordinal)
            .ToList();
    }

    public static MapStatEntry BuildOverall(IReadOnlyList<MatchStatInput> inputs)
    {
        if (inputs.Count == 0)
            return new MapStatEntry { Map = OverallMap };

        var entry = BuildEntry(inputs);
        entry.Map = OverallMap;
        return entry;
    }

    private static MapStatEntry BuildEntry(IReadOnlyList<MatchStatInput> matches)
    {
        var games = matches.Count;
        var wins = matches.Count(m => m.Won);

        // Per-minute rates (GPM/XPM/DPM) are aggregate rates over the games that
        // actually carry the relevant breakdown: total value divided by total
        // minutes played in those games. Each field has its own denominator —
        // nulls are skipped from both numerator and minutes. Null when no game
        // carries the breakdown. Zero-duration games are excluded to avoid
        // divide-by-zero.
        var avgGpm = PerMinuteRate(matches, m => m.GoldEarned);
        var avgXpm = PerMinuteRate(matches, m => m.Experience);
        var avgDpm = PerMinuteRate(matches, m => m.HeroDamage);

        return new MapStatEntry
        {
            Map = matches[0].Map,
            Games = games,
            AvgKills = matches.Sum(m => m.Kills) / (double)games,
            AvgDeaths = matches.Sum(m => m.Deaths) / (double)games,
            AvgAssists = matches.Sum(m => m.Assists) / (double)games,
            AvgWards = matches.Sum(m => m.WardsPlaced) / (double)games,
            AvgGPM = avgGpm,
            AvgXPM = avgXpm,
            AvgDPM = avgDpm,
            Wins = wins,
            WinRate = (double)wins / games,
        };
    }

    private static double? PerMinuteRate(
        IReadOnlyList<MatchStatInput> matches,
        Func<MatchStatInput, int?> selector
    )
    {
        var bearingGames = matches
            .Where(m => selector(m).HasValue && m.DurationSeconds > 0)
            .ToList();
        var minutes = bearingGames.Sum(m => m.DurationSeconds / 60.0);
        return bearingGames.Count > 0 ? bearingGames.Sum(m => selector(m)!.Value) / minutes : null;
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

    /// <summary>
    /// A degenerate replay has 0 or 1 snapshots — the replay parser hasn't finished
    /// processing the demo file yet (happens when queried too soon after match end).
    /// A healthy replay always has many snapshots at ~30s intervals starting at Time=-90.
    /// </summary>
    public static bool IsDegenerate(ParsedReplay replay) => replay.Snapshots.Count <= 1;

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
        var orderedSnapshots = replay
            .Snapshots.Select((snapshot, index) => new { snapshot, index })
            .OrderBy(x => x.snapshot.Time)
            .ThenBy(x => x.index)
            .Select(x => x.snapshot);

        foreach (var snapshot in orderedSnapshots)
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

public static class SkillBuildAggregator
{
    public static bool IsDegenerate(ParsedReplay replay) =>
        ItemTimingAggregator.IsDegenerate(replay);

    public static IReadOnlyList<SkillLevelEvent> Build(ParsedReplay replay)
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

        var events = new List<SkillLevelEvent>();
        var orderedSnapshots = replay
            .Snapshots.Select((snapshot, index) => new { snapshot, index })
            .OrderBy(x => x.snapshot.Time)
            .ThenBy(x => x.index)
            .Select(x => x.snapshot);

        foreach (var snapshot in orderedSnapshots)
        {
            for (var ti = 0; ti < snapshot.Teams.Count; ti++)
            {
                var players = snapshot.Teams[ti].Players;
                for (var pi = 0; pi < players.Count; pi++)
                {
                    if (!positionToAccount.TryGetValue((ti, pi), out var account))
                        continue;

                    foreach (var skill in players[pi].Skills)
                    {
                        events.Add(
                            new SkillLevelEvent
                            {
                                GameId = replay.GameId,
                                AccountId = account,
                                SkillId = skill.SkillId,
                                Level = skill.Level,
                                TimeSeconds = snapshot.Time,
                            }
                        );
                    }
                }
            }
        }

        return events;
    }
}

// getmatchsummary's heroDamage is a dead field (always 0); the real cumulative
// hero damage per player only lives in parsedReplay snapshots. This returns the
// per-account end-game total (the max reached, since the value is monotonic).
public static class HeroDamageAggregator
{
    public static IReadOnlyDictionary<Guid, int> Build(ParsedReplay? replay)
    {
        if (replay is null || replay.Snapshots.Count == 0)
            return new Dictionary<Guid, int>();

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
            return new Dictionary<Guid, int>();

        var max = new Dictionary<Guid, int>();
        foreach (var snapshot in replay.Snapshots)
        {
            for (var ti = 0; ti < snapshot.Teams.Count; ti++)
            {
                var players = snapshot.Teams[ti].Players;
                for (var pi = 0; pi < players.Count; pi++)
                {
                    if (!positionToAccount.TryGetValue((ti, pi), out var account))
                        continue;
                    var hd = players[pi].HeroDamage;
                    if (hd is int v && v > max.GetValueOrDefault(account))
                        max[account] = v;
                }
            }
        }

        return max;
    }
}
