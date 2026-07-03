using System.Globalization;
using HonStats.App.Players;
using HonStats.App.Records;
using HonStats.App.ReferenceData;
using HonStats.Domain.ReferenceData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

// Records = community single-match-performance leaderboards. Local SQLite
// aggregation (no juvio call): IRecordsQuery ranks match_roster metrics, then we
// enrich each row with the resolved player name (for the profile link + flag) and
// hero icon/name from cached reference data — same enrichment pattern as the
// match-detail and leaderboard pages.
public class RecordsModel(
    IRecordsQuery records,
    IReferenceDataQuery reference,
    IPlayerNameResolver nameResolver
) : PageModel
{
    private const string DefaultMap = "All";
    private const string DefaultPeriod = "all";
    private static readonly HashSet<string> ValidMaps = ["All", "ForestsOfCaldavar", "MidWars"];
    private static readonly HashSet<string> ValidPeriods = ["all", "30d"];

    public string Map { get; private set; } = DefaultMap;
    public string Period { get; private set; } = DefaultPeriod;
    public IReadOnlyList<RecordCategoryView> Categories { get; private set; } = [];

    public async Task OnGet(string? map, string? period, CancellationToken ct)
    {
        this.Map = NormalizeMap(map);
        this.Period = NormalizePeriod(period);

        var view = await records.GetAsync(this.Map, this.Period, ct);

        // One bulk resolve across every category — covers all top-N accountIds in
        // a single chunked call (the resolver batches ≤50 internally).
        var accountIds = view
            .Categories.SelectMany(c => c.Rows.Select(r => r.AccountId))
            .Distinct()
            .ToList();
        var names = await nameResolver.ResolveAsync(accountIds, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);

        this.Categories = view
            .Categories.Select(c => new RecordCategoryView(
                c.Id,
                c.Title,
                c.Subtitle,
                c.Format,
                c.Rows.Select(r => BuildRow(r, names, heroes)).ToList()
            ))
            .ToList();
    }

    private static RecordRowView BuildRow(
        RecordRow r,
        IReadOnlyDictionary<Guid, ResolvedName> names,
        IReadOnlyDictionary<int, Hero> heroes
    )
    {
        names.TryGetValue(r.AccountId, out var name);
        heroes.TryGetValue(r.HeroId ?? 0, out var hero);
        return new RecordRowView(
            r.Rank,
            r.AccountId,
            r.GameId,
            name?.DisplayName ?? name?.Username ?? r.AccountId.ToString(),
            name?.Username ?? name?.DisplayName ?? r.AccountId.ToString(),
            name?.Country,
            hero?.DisplayName ?? "Unknown hero",
            hero?.IconUrl,
            r.Value,
            r.DurationSeconds,
            r.Won,
            r.Date
        );
    }

    public static string MapLabel(string map) =>
        map switch
        {
            "All" => "All maps",
            "ForestsOfCaldavar" => "Forests of Caldavar",
            "MidWars" => "Mid Wars",
            _ => map,
        };

    public static string PeriodLabel(string period) =>
        period switch
        {
            "30d" => "Last 30 days",
            _ => "All time",
        };

    // Value formatting is driven by the category Format. Integer metrics render
    // with thousands separators (net worth / damage hit 5 digits); KDA to 2 dp;
    // duration (seconds) as M:SS.
    public static string FormatValue(RecordFormat format, double value) =>
        format switch
        {
            RecordFormat.Kda => value.ToString("0.00", CultureInfo.InvariantCulture),
            RecordFormat.Duration => FormatSeconds((int)value),
            _ => ((long)value).ToString("N0", CultureInfo.InvariantCulture),
        };

    public static string FormatSeconds(int seconds) => $"{seconds / 60}:{seconds % 60:D2}";

    public static string ValueColumnHeader(RecordFormat format) =>
        format switch
        {
            RecordFormat.Kda => "KDA",
            RecordFormat.Duration => "Time",
            _ => "Record",
        };

    private static string NormalizeMap(string? map) =>
        !string.IsNullOrEmpty(map) && ValidMaps.Contains(map) ? map : DefaultMap;

    private static string NormalizePeriod(string? period) =>
        !string.IsNullOrEmpty(period) && ValidPeriods.Contains(period) ? period : DefaultPeriod;
}

public sealed record RecordCategoryView(
    string Id,
    string Title,
    string Subtitle,
    RecordFormat Format,
    IReadOnlyList<RecordRowView> Rows
);

public sealed record RecordRowView(
    int Rank,
    Guid AccountId,
    int GameId,
    string DisplayName,
    string Username,
    string? Country,
    string HeroName,
    string? HeroIconUrl,
    double Value,
    int DurationSeconds,
    bool Won,
    DateTimeOffset Date
);
