using HonStats.App.Indexing;
using HonStats.App.Insights;
using HonStats.App.Matches;
using HonStats.App.Players;
using HonStats.App.ReferenceData;
using HonStats.Domain.Indexing;
using HonStats.Domain.Insights;
using HonStats.Domain.Matches;
using HonStats.Domain.Players;
using HonStats.Domain.ReferenceData;
using HonStats.Infra.Insights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace HonStats.Web.Pages;

public class ProfileModel(
    IPlayerProfileQuery profiles,
    IMatchQuery matches,
    IParsedReplayQuery parsedReplays,
    IPlayerInsightsQuery insights,
    IInsightsRawQuery rawQuery,
    IReferenceDataQuery reference,
    IPlayerNameResolver nameResolver,
    IPlayerSearch playerSearch,
    IReindexQueue queue,
    IIndexProgressTracker progressTracker,
    IOptions<TierTimingOptions> tierTiming,
    IMmrHistoryQuery mmrHistory
) : PageModel
{
    public const int PageSize = 25;

    public Guid AccountId { get; set; }
    public string? Username { get; set; }
    public string Tab { get; set; } = "matches";
    public string SelectedMap { get; set; } = "all";
    public PlayerProfile? Profile { get; set; }
    public IReadOnlyList<MmrSnapshot> MmrHistory { get; set; } = [];
    public IndexedPlayer? Indexed { get; set; }
    public Dictionary<int, Hero> Heroes { get; set; } = new();

    public IReadOnlyList<PlayerMatch> RecentMatches { get; set; } = [];
    public bool HasMoreMatches { get; set; }
    public IReadOnlyList<TeammateStat> Teammates { get; set; } = [];
    public bool HasMoreTeammates { get; set; }
    public IReadOnlyDictionary<int, int> HeroGames { get; set; } = new Dictionary<int, int>();
    public IReadOnlyList<HeroBuildEntry> HeroBuild { get; set; } = [];
    public IReadOnlyList<ItemTimingEntry> HeroItemTiming { get; set; } = [];
    public IReadOnlyList<MapStatEntry> MapStats { get; set; } = [];
    public MapStatEntry? MapOverview { get; set; }
    public Dictionary<int, Item> Items { get; set; } = new();
    public int SelectedHeroId { get; set; }
    public IReadOnlyList<LoadoutEntry> Loadouts { get; set; } = [];

    // Tier time-thresholds exposed for the Razor view so it can call
    // GuideTierClassifier.ClassifyByTime. Sourced from IOptions<TierTimingOptions>
    // (config section HonStats:Insights:TierTiming).
    public int EarlyCutoffMinutes => tierTiming.Value.EarlyCutoffMinutes;
    public int LateCutoffMinutes => tierTiming.Value.LateCutoffMinutes;

    public async Task<IActionResult> OnGet(
        string username,
        string tab = "matches",
        string map = "all",
        int heroId = 0,
        CancellationToken ct = default
    )
    {
        var results = await playerSearch.SearchAsync(username, ct);
        if (results.Count == 0)
            return NotFound();

        this.AccountId = results[0].AccountId;
        this.Username = username;
        this.Tab = tab;
        this.SelectedMap = map;

        this.Indexed = await insights.GetIndexedPlayerAsync(this.AccountId, ct);
        this.Heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);

        // Always loaded — juvio responses are cached server-side (60s for stats,
        // 1h for rank/identity), so repeated calls within the TTL are free.
        this.Profile = await profiles.GetAsync(this.AccountId, ct);
        this.MmrHistory = await mmrHistory.GetAsync(this.AccountId, ct: ct);

        this.MapStats = await insights.GetMapStatsAsync(this.AccountId, ct);
        this.MapOverview =
            this.SelectedMap == "all"
                ? await insights.GetOverallStatsAsync(this.AccountId, ct)
                : this.MapStats.FirstOrDefault(e => e.Map == this.SelectedMap);

        switch (tab)
        {
            case "teammates":
                this.Teammates = await insights.GetTeammatesAsync(
                    this.AccountId,
                    PageSize,
                    0,
                    MapFilter(this.SelectedMap),
                    ct
                );
                this.HasMoreTeammates = this.Teammates.Count == PageSize;
                break;
            case "heroBuilds":
                this.Items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
                this.HeroGames = await insights.GetHeroGamesAsync(
                    this.AccountId,
                    MapFilter(this.SelectedMap),
                    ct
                );
                this.SelectedHeroId =
                    heroId == 0 && this.HeroGames.Count > 0
                        ? this.HeroGames.OrderByDescending(kv => kv.Value).First().Key
                        : heroId;
                if (this.SelectedHeroId > 0)
                {
                    var mapFilter = MapFilter(this.SelectedMap);
                    var buildInputs = await rawQuery.GetHeroItemsBoughtAsync(
                        this.AccountId,
                        this.SelectedHeroId,
                        mapFilter,
                        ct
                    );
                    this.HeroBuild = HeroBuildAggregator.Build(buildInputs);
                    this.HeroItemTiming = await insights.GetHeroItemTimingAsync(
                        this.AccountId,
                        this.SelectedHeroId,
                        ct
                    );
                    var loadoutInputs = await rawQuery.GetHeroItemInputsAsync(
                        this.AccountId,
                        this.SelectedHeroId,
                        mapFilter,
                        ct
                    );
                    this.Loadouts = LoadoutAggregator.Build(
                        loadoutInputs,
                        ResolveConsumableItemIds()
                    );
                }
                break;
            default:
                this.Tab = "matches";
                var fetched = await matches.GetRecentForPlayerAsync(
                    this.AccountId,
                    PageSize,
                    0,
                    ct
                );
                // The juvio match query has no server-side map filter, so filter
                // post-fetch. HasMore reflects the unfiltered page so pagination
                // keeps walking the full history across sparse per-map slices.
                this.RecentMatches =
                    this.SelectedMap == "all"
                        ? fetched
                        : fetched.Where(m => m.Map == this.SelectedMap).ToList();
                this.HasMoreMatches = fetched.Count == PageSize;
                break;
        }

        return Page();
    }

    public async Task<IActionResult> OnGetMatchesMore(
        Guid accountId,
        int offset,
        string? map = null,
        CancellationToken ct = default
    )
    {
        var fetched = await matches.GetRecentForPlayerAsync(accountId, PageSize, offset, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        // Post-fetch map filter mirrors OnGet: the juvio query has no server-side
        // map param. SelectedMap is threaded into the view so row partials can carry
        // &map= in their own pagination URLs (Wired by T-UI-ROWS).
        var selectedMap = string.IsNullOrEmpty(map) ? "all" : map;
        var recent =
            selectedMap == "all" ? fetched : fetched.Where(m => m.Map == selectedMap).ToList();
        return Partial(
            "Players/Partials/_MatchesRows",
            new MatchesView(
                accountId,
                recent,
                heroes,
                offset,
                PageSize,
                fetched.Count == PageSize,
                selectedMap
            )
        );
    }

    public async Task<IActionResult> OnGetTeammatesMore(
        Guid accountId,
        int offset,
        string? map = null,
        CancellationToken ct = default
    )
    {
        var selectedMap = string.IsNullOrEmpty(map) ? "all" : map;
        var teammates = await insights.GetTeammatesAsync(
            accountId,
            PageSize,
            offset,
            MapFilter(selectedMap),
            ct
        );
        return Partial(
            "Players/Partials/_TeammatesRows",
            new TeammatesView(
                accountId,
                teammates,
                offset,
                PageSize,
                teammates.Count == PageSize,
                selectedMap
            )
        );
    }

    public async Task<IActionResult> OnGetMatchDetail(
        Guid accountId,
        int gameId,
        CancellationToken ct = default
    )
    {
        var detail = await matches.GetSummaryAsync(gameId, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        var items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
        var names = detail is null
            ? new Dictionary<Guid, ResolvedName>()
            : await nameResolver.ResolveAsync(detail.Players.Select(p => p.AccountId).ToList(), ct);
        var heroDamage = HeroDamageAggregator.Build(await parsedReplays.GetAsync(gameId, ct));
        return Partial(
            "Shared/Partials/_MatchDetail",
            new MatchDetailView(detail, heroes, items, names, heroDamage)
        );
    }

    public async Task<IActionResult> OnPostReindex(Guid accountId, CancellationToken ct = default)
    {
        var result = await queue.RequestReindexAsync(accountId, forceBackfill: false, ct);
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        return Partial(
            "Players/Partials/_ReindexButton",
            new ReindexButtonView(accountId, result, indexed)
        );
    }

    public async Task<IActionResult> OnGetIndexProgress(
        Guid accountId,
        bool wasIndexing = false,
        CancellationToken ct = default
    )
    {
        var p = progressTracker.GetProgress(accountId);
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);

        // When indexing completes (was Indexing, now Indexed), refresh the whole
        // page so stats/matches/builds/loadouts re-render with the new data.
        if (wasIndexing && indexed?.Status == IndexingStatus.Indexed)
        {
            Response.Headers["HX-Refresh"] = "true";
            return Content("");
        }

        return Partial(
            "Players/Partials/_ReindexButton",
            new ReindexButtonView(accountId, null, indexed, p.Fetched, p.Total, p.Done, p.StartedAt)
        );
    }

    // Consumable item ids used to strip potions/runes/etc. from final-inventory
    // loadouts. Derived from the same reference fetch that populates Items, so this
    // is only valid after the heroBuilds tab has loaded Items. Opaque to the
    // LoadoutAggregator, which just treats it as an exclusion set.
    private ISet<int> ResolveConsumableItemIds() =>
        this
            .Items.Values.Where(i => i.ShopCategories.Contains("consumable"))
            .Select(i => i.Id)
            .ToHashSet();

    public static string Flag(string? country)
    {
        if (string.IsNullOrEmpty(country) || country.Length != 2)
            return string.Empty;
        var u = country.ToUpperInvariant();
        if (!char.IsLetter(u[0]) || !char.IsLetter(u[1]))
            return string.Empty;
        return char.ConvertFromUtf32(0x1F1E6 + (u[0] - 'A'))
            + char.ConvertFromUtf32(0x1F1E6 + (u[1] - 'A'));
    }

    public static string Stars(int level) => level > 0 ? new string('★', level) : string.Empty;

    public static string MapLabel(string map) =>
        map switch
        {
            "ForestsOfCaldavar" => "FoC",
            "MidWars" => "MW",
            _ => map,
        };

    private static string? MapFilter(string map) => map == "all" ? null : map;

    // Formats a mean first-buy second as M:SS. Pre-game purchases (negative seconds,
    // during the pre-creep phase) clamp to 0:00.
    public static string FormatBuyTime(double seconds)
    {
        var clamped = Math.Max(0, (int)Math.Round(seconds));
        return $"{clamped / 60}:{clamped % 60:D2}";
    }

    public static double PerMinute(double total, int durationSeconds) =>
        durationSeconds > 0 ? total * 60.0 / durationSeconds : 0;

    public static string RoleLabel(int roleIndex) =>
        roleIndex switch
        {
            1 => "Carry",
            2 => "Mid",
            3 => "Offlane",
            4 => "Soft Support",
            5 => "Hard Support",
            6 => "Solo Offlane",
            7 => "Jungle",
            _ => "Unassigned",
        };

    public static string RoleColor(int roleIndex) =>
        roleIndex switch
        {
            1 => "#5591ff",
            2 => "#68fff6",
            3 => "#e462ff",
            4 => "#ffff3b",
            5 => "#ffc95a",
            6 => "#a647ba",
            7 => "#b5ab24",
            _ => "#ffffff",
        };

    public static string RoleSlug(int roleIndex) =>
        roleIndex switch
        {
            1 => "carry",
            2 => "mid",
            3 => "offlane",
            4 => "softsupport",
            5 => "hardsupport",
            6 => "soloofflane",
            7 => "jungle",
            _ => "unassigned",
        };
}

public record MatchesView(
    Guid AccountId,
    IReadOnlyList<PlayerMatch> Matches,
    Dictionary<int, Hero> Heroes,
    int Offset,
    int PageSize,
    bool HasMore,
    string SelectedMap = "all"
);

public record TeammatesView(
    Guid AccountId,
    IReadOnlyList<TeammateStat> Teammates,
    int Offset,
    int PageSize,
    bool HasMore,
    string SelectedMap = "all"
);

public record MatchDetailView(
    MatchDetail? Detail,
    Dictionary<int, Hero> Heroes,
    Dictionary<int, Item> Items,
    IReadOnlyDictionary<Guid, ResolvedName> Names,
    IReadOnlyDictionary<Guid, int> HeroDamageByAccount
);

public record ReindexButtonView(
    Guid AccountId,
    ReindexResult? Result,
    IndexedPlayer? Indexed,
    int Fetched = 0,
    int Total = 0,
    bool ProgressDone = false,
    DateTimeOffset? ProgressStartedAt = null
);
