namespace HonStats.App.Records;

// Local SQLite aggregation over match_roster (per-match-per-player metrics) joined
// to player_matches (per-game facts: duration/map/date). Not a juvio call — the
// raw metrics are already indexed. See SqliteRecordsQuery for the implementation.
public interface IRecordsQuery
{
    Task<RecordsView> GetAsync(string map, string period, CancellationToken ct = default);
}

// How a category's numeric Value should be rendered.
public enum RecordFormat
{
    Integer, // kills, assists, CS, net worth, hero/building damage
    Kda, // (K+A)/D — two decimals
    Duration, // match length in seconds — M:SS
}

// The full page payload: the active filters plus the per-category top-N boards.
// Categories are ordered by the query in the display order the page renders.
public sealed record RecordsView(
    string Map,
    string Period,
    IReadOnlyList<RecordCategory> Categories
);

// One leaderboard board (e.g. "Most Kills"). Rows are already sorted + Take(N)
// with a 1-based Rank assigned by the query.
public sealed record RecordCategory(
    string Id,
    string Title,
    string Subtitle,
    RecordFormat Format,
    IReadOnlyList<RecordRow> Rows
);

// A single ranked performance. Value is the category metric (double so it can hold
// KDA fractions); DurationSeconds carries the match length for the duration boards
// (and is also shown as context on the other boards). HeroId may be null for very
// old games that pre-date HeroId backfill.
public sealed record RecordRow(
    int Rank,
    Guid AccountId,
    int GameId,
    int? HeroId,
    double Value,
    int DurationSeconds,
    bool Won,
    DateTimeOffset Date
);
