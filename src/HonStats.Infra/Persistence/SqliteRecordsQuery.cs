using System.Globalization;
using HonStats.App.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HonStats.Infra.Persistence;

// Records = "best single-match performances" across the whole indexed corpus:
// match_roster (per-match-per-player metrics) enriched with each game's
// Duration/Map/Date from player_matches.
//
// Computed in memory, not in SQL: the SQLite EF provider only translates ==/!=
// for DateTimeOffset (not >=, ordering, or Distinct), so the date filter and the
// per-game de-duplication can't run server-side. The corpus is small (~26k roster
// rows), so both tables are pulled with plain translatable queries and the
// stitch/filter/rank happens with LINQ-to-Objects. Results are cached per
// (map, period) for 5 minutes — the underlying data only changes on (re)indexing.
internal sealed class SqliteRecordsQuery(
    IDbContextFactory<HonStatsDbContext> dbFactory,
    IMemoryCache cache
) : IRecordsQuery
{
    private const int TopN = 15;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<RecordsView> GetAsync(
        string map,
        string period,
        CancellationToken ct = default
    ) =>
        await cache.GetOrCreateAsync(
            $"records:{map}:{period}",
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheTtl;
                return await BuildAsync(map, period, ct);
            }
        ) ?? new RecordsView(map, period, []);

    private async Task<RecordsView> BuildAsync(string map, string period, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var since =
            period == PeriodMonth ? DateTimeOffset.UtcNow.AddDays(-30) : (DateTimeOffset?)null;
        var mapFilter = map == MapAll ? null : map;

        // One canonical fact row per game. Every player_matches row for a GameId
        // carries the identical Duration/Map/Date, so grouping by GameId collapses
        // them; done in memory because a server-side Distinct over a projection
        // containing DateTimeOffset isn't translatable on SQLite.
        var gameFacts = (
            await db
                .PlayerMatches.Select(pm => new
                {
                    pm.GameId,
                    pm.Duration,
                    pm.Map,
                    pm.Date,
                })
                .ToListAsync(ct)
        ).GroupBy(g => g.GameId).ToDictionary(g => g.Key, g => g.First());

        var rosterRows = await db
            .MatchRoster.Select(r => new
            {
                r.AccountId,
                r.GameId,
                r.HeroId,
                r.Won,
                r.Kills,
                r.Deaths,
                r.Assists,
                r.NetWorth,
                r.CreepKills,
                r.NeutralKills,
                r.HeroDamage,
                r.BuildingDamage,
            })
            .ToListAsync(ct);

        var facts = new List<RosterFact>(rosterRows.Count);
        foreach (var r in rosterRows)
        {
            // Inner-join semantics: skip roster rows with no matching game fact.
            if (!gameFacts.TryGetValue(r.GameId, out var g))
                continue;
            if (mapFilter is not null && g.Map != mapFilter)
                continue;
            if (since is not null && g.Date < since.Value)
                continue;

            facts.Add(
                new RosterFact
                {
                    AccountId = r.AccountId,
                    GameId = r.GameId,
                    HeroId = r.HeroId,
                    Won = r.Won,
                    Kills = r.Kills,
                    Deaths = r.Deaths,
                    Assists = r.Assists,
                    NetWorth = r.NetWorth,
                    CreepKills = r.CreepKills,
                    NeutralKills = r.NeutralKills,
                    HeroDamage = r.HeroDamage,
                    BuildingDamage = r.BuildingDamage,
                    Duration = g.Duration,
                    Date = g.Date,
                    Map = g.Map,
                }
            );
        }

        var categories = new List<RecordCategory>
        {
            new(
                "kills",
                "Most Kills",
                "Single-game kill count",
                RecordFormat.Integer,
                Run(
                    facts,
                    f => f.Kills != null,
                    f => f.Kills!.Value,
                    descending: true,
                    winnersOnly: false
                )
            ),
            new(
                "assists",
                "Most Assists",
                "Single-game assist count",
                RecordFormat.Integer,
                Run(
                    facts,
                    f => f.Assists != null,
                    f => f.Assists!.Value,
                    descending: true,
                    winnersOnly: false
                )
            ),
            new(
                "kda",
                "Highest KDA",
                "(Kills + Assists) / Deaths",
                RecordFormat.Kda,
                Run(
                    facts,
                    f => f.Kills != null && f.Assists != null && f.Deaths != null && f.Deaths > 0,
                    f => (f.Kills!.Value + f.Assists!.Value) / (double)f.Deaths!.Value,
                    descending: true,
                    winnersOnly: false
                )
            ),
            new(
                "cs",
                "Most Creep Score",
                "Creep kills + neutral kills",
                RecordFormat.Integer,
                Run(
                    facts,
                    f => f.CreepKills != null && f.NeutralKills != null,
                    f => f.CreepKills!.Value + f.NeutralKills!.Value,
                    descending: true,
                    winnersOnly: false
                )
            ),
            new(
                "hero-damage",
                "Most Hero Damage",
                "Damage dealt to heroes",
                RecordFormat.Integer,
                Run(
                    facts,
                    f => f.HeroDamage != null,
                    f => f.HeroDamage!.Value,
                    descending: true,
                    winnersOnly: false
                )
            ),
            new(
                "building-damage",
                "Most Building Damage",
                "Damage dealt to structures",
                RecordFormat.Integer,
                Run(
                    facts,
                    f => f.BuildingDamage != null,
                    f => f.BuildingDamage!.Value,
                    descending: true,
                    winnersOnly: false
                )
            ),
            new(
                "net-worth",
                "Highest Net Worth",
                "End-of-game net worth",
                RecordFormat.Integer,
                Run(
                    facts,
                    f => f.NetWorth != null,
                    f => f.NetWorth!.Value,
                    descending: true,
                    winnersOnly: false
                )
            ),
            new(
                "fastest-win",
                "Fastest Win",
                "Match length, winners only",
                RecordFormat.Duration,
                Run(facts, f => true, f => f.Duration, descending: false, winnersOnly: true)
            ),
            new(
                "longest-match",
                "Longest Match",
                "Match length",
                RecordFormat.Duration,
                Run(facts, f => true, f => f.Duration, descending: true, winnersOnly: false)
            ),
        };

        return new RecordsView(map, period, categories);
    }

    // Builds one top-N board: drop rows where the metric is absent (old games
    // ingested before a column existed stay null — never a lying 0), optionally
    // restrict to winners (Fastest Win), order, take N, then assign 1-based rank.
    private static IReadOnlyList<RecordRow> Run<T>(
        IReadOnlyList<RosterFact> facts,
        Func<RosterFact, bool> hasMetric,
        Func<RosterFact, T> metric,
        bool descending,
        bool winnersOnly
    )
        where T : struct
    {
        IEnumerable<RosterFact> q = facts.Where(hasMetric);
        if (winnersOnly)
            q = q.Where(f => f.Won);
        q = descending ? q.OrderByDescending(metric) : q.OrderBy(metric);

        return q.Take(TopN)
            .Select(
                (f, i) =>
                    new RecordRow(
                        i + 1,
                        f.AccountId,
                        f.GameId,
                        f.HeroId,
                        Convert.ToDouble(metric(f), CultureInfo.InvariantCulture),
                        f.Duration,
                        f.Won,
                        f.Date
                    )
            )
            .ToList();
    }

    // Constants kept here so the App-layer port stays free of magic strings; the
    // page model validates and normalizes before calling GetAsync.
    public const string MapAll = "All";
    public const string PeriodMonth = "30d";
    public const string PeriodAll = "all";

    // In-memory join shape: a roster row enriched with its game's Duration/Map/Date.
    private sealed class RosterFact
    {
        public Guid AccountId { get; init; }
        public int GameId { get; init; }
        public int? HeroId { get; init; }
        public bool Won { get; init; }
        public int? Kills { get; init; }
        public int? Deaths { get; init; }
        public int? Assists { get; init; }
        public int? NetWorth { get; init; }
        public int? CreepKills { get; init; }
        public int? NeutralKills { get; init; }
        public int? HeroDamage { get; init; }
        public int? BuildingDamage { get; init; }
        public int Duration { get; init; }
        public DateTimeOffset Date { get; init; }
        public string Map { get; init; } = string.Empty;
    }
}
