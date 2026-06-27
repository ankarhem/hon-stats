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
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class ProfileModel(
    IPlayerProfileQuery profiles,
    IMatchQuery matches,
    IPlayerInsightsQuery insights,
    IReferenceDataQuery reference,
    IPlayerNameResolver nameResolver,
    IPlayerSearch playerSearch,
    IReindexQueue queue,
    IIndexProgressTracker progressTracker
) : PageModel
{
    public const int PageSize = 25;

    public Guid AccountId { get; set; }
    public string? Username { get; set; }
    public string Tab { get; set; } = "matches";
    public PlayerProfile? Profile { get; set; }
    public IndexedPlayer? Indexed { get; set; }
    public Dictionary<int, Hero> Heroes { get; set; } = new();

    public IReadOnlyList<PlayerMatch> RecentMatches { get; set; } = [];
    public bool HasMoreMatches { get; set; }
    public IReadOnlyList<TeammateStat> Teammates { get; set; } = [];
    public bool HasMoreTeammates { get; set; }
    public IReadOnlyDictionary<int, int> HeroGames { get; set; } = new Dictionary<int, int>();
    public IReadOnlyList<HeroBuildEntry> HeroBuild { get; set; } = [];
    public IReadOnlyList<HeroItemPairEntry> HeroItemPairs { get; set; } = [];
    public IReadOnlyList<ItemTimingEntry> HeroItemTiming { get; set; } = [];
    public IReadOnlyList<MapStatEntry> MapStats { get; set; } = [];
    public Dictionary<int, Item> Items { get; set; } = new();
    public int SelectedHeroId { get; set; }

    public async Task<IActionResult> OnGet(
        string username,
        string tab = "matches",
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

        this.Indexed = await insights.GetIndexedPlayerAsync(this.AccountId, ct);
        this.Heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);

        if (!Request.IsHtmx())
        {
            this.Profile = await profiles.GetAsync(this.AccountId, ct);
        }

        switch (tab)
        {
            case "teammates":
                this.Teammates = await insights.GetTeammatesAsync(this.AccountId, PageSize, 0, ct);
                this.HasMoreTeammates = this.Teammates.Count == PageSize;
                break;
            case "heroBuilds":
                this.Items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
                this.HeroGames = await insights.GetHeroGamesAsync(this.AccountId, ct);
                this.SelectedHeroId =
                    heroId == 0 && this.HeroGames.Count > 0
                        ? this.HeroGames.OrderByDescending(kv => kv.Value).First().Key
                        : heroId;
                if (this.SelectedHeroId > 0)
                {
                    this.HeroBuild = await insights.GetHeroBuildAsync(
                        this.AccountId,
                        this.SelectedHeroId,
                        ct
                    );
                    this.HeroItemPairs = await insights.GetHeroItemPairsAsync(
                        this.AccountId,
                        this.SelectedHeroId,
                        10,
                        ct
                    );
                    this.HeroItemTiming = await insights.GetHeroItemTimingAsync(
                        this.AccountId,
                        this.SelectedHeroId,
                        ct
                    );
                }
                break;
            case "maps":
                this.MapStats = await insights.GetMapStatsAsync(this.AccountId, ct);
                break;
            default:
                this.Tab = "matches";
                this.RecentMatches = await matches.GetRecentForPlayerAsync(
                    this.AccountId,
                    PageSize,
                    0,
                    ct
                );
                this.HasMoreMatches = this.RecentMatches.Count == PageSize;
                break;
        }

        return Request.IsHtmx() ? Partial("_ProfileRegion", this) : Page();
    }

    public async Task<IActionResult> OnGetMatchesMore(
        Guid accountId,
        int offset,
        CancellationToken ct = default
    )
    {
        var recent = await matches.GetRecentForPlayerAsync(accountId, PageSize, offset, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        return Partial(
            "_MatchesRows",
            new MatchesView(accountId, recent, heroes, offset, PageSize, recent.Count == PageSize)
        );
    }

    public async Task<IActionResult> OnGetTeammatesMore(
        Guid accountId,
        int offset,
        CancellationToken ct = default
    )
    {
        var teammates = await insights.GetTeammatesAsync(accountId, PageSize, offset, ct);
        return Partial(
            "_TeammatesRows",
            new TeammatesView(accountId, teammates, offset, PageSize, teammates.Count == PageSize)
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
        return Partial("_MatchDetail", new MatchDetailView(detail, heroes, items, names));
    }

    public async Task<IActionResult> OnPostReindex(Guid accountId, CancellationToken ct = default)
    {
        var result = await queue.RequestReindexAsync(accountId, forceBackfill: false, ct);
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        return Partial("_ReindexButton", new ReindexButtonView(accountId, result, indexed));
    }

    public async Task<IActionResult> OnGetIndexProgress(
        Guid accountId,
        CancellationToken ct = default
    )
    {
        var p = progressTracker.GetProgress(accountId);
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        return Partial(
            "_ReindexButton",
            new ReindexButtonView(accountId, null, indexed, p.Fetched, p.Total, p.Done, p.StartedAt)
        );
    }

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

    // Formats a mean first-buy second as M:SS. Pre-game purchases (negative seconds,
    // during the pre-creep phase) clamp to 0:00.
    public static string FormatBuyTime(double seconds)
    {
        var clamped = Math.Max(0, (int)Math.Round(seconds));
        return $"{clamped / 60}:{clamped % 60:D2}";
    }
}

public record MatchesView(
    Guid AccountId,
    IReadOnlyList<PlayerMatch> Matches,
    Dictionary<int, Hero> Heroes,
    int Offset,
    int PageSize,
    bool HasMore
);

public record TeammatesView(
    Guid AccountId,
    IReadOnlyList<TeammateStat> Teammates,
    int Offset,
    int PageSize,
    bool HasMore
);

public record MatchDetailView(
    MatchDetail? Detail,
    Dictionary<int, Hero> Heroes,
    Dictionary<int, Item> Items,
    IReadOnlyDictionary<Guid, ResolvedName> Names
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
