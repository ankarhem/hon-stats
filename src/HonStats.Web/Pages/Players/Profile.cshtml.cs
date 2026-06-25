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
    IReindexQueue queue,
    IIndexProgressTracker progressTracker
) : PageModel
{
    public const int PageSize = 25;

    public Guid AccountId { get; set; }
    public string Tab { get; set; } = "matches";
    public PlayerProfile? Profile { get; set; }
    public IndexedPlayer? Indexed { get; set; }
    public Dictionary<int, Hero> Heroes { get; set; } = new();

    public IReadOnlyList<PlayerMatch> RecentMatches { get; set; } = [];
    public bool HasMoreMatches { get; set; }
    public IReadOnlyList<TeammateStat> Teammates { get; set; } = [];
    public IReadOnlyDictionary<int, int> HeroGames { get; set; } = new Dictionary<int, int>();
    public IReadOnlyList<HeroBuildEntry> HeroBuild { get; set; } = [];
    public Dictionary<int, Item> Items { get; set; } = new();
    public int SelectedHeroId { get; set; }

    public async Task<IActionResult> OnGet(
        Guid accountId,
        string tab = "matches",
        int heroId = 0,
        CancellationToken ct = default
    )
    {
        this.AccountId = accountId;
        this.Tab = tab;

        this.Indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        this.Heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);

        if (!Request.IsHtmx())
        {
            this.Profile = await profiles.GetAsync(accountId, ct);
        }

        switch (tab)
        {
            case "teammates":
                this.Teammates = await insights.GetTeammatesAsync(accountId, ct);
                break;
            case "heroBuilds":
                this.Items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
                this.HeroGames = await insights.GetHeroGamesAsync(accountId, ct);
                this.SelectedHeroId =
                    heroId == 0 && this.HeroGames.Count > 0
                        ? this.HeroGames.OrderByDescending(kv => kv.Value).First().Key
                        : heroId;
                if (this.SelectedHeroId > 0)
                {
                    this.HeroBuild = await insights.GetHeroBuildAsync(
                        accountId,
                        this.SelectedHeroId,
                        ct
                    );
                }
                break;
            default:
                this.Tab = "matches";
                this.RecentMatches = await matches.GetRecentForPlayerAsync(
                    accountId,
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

    public async Task<IActionResult> OnGetMatchDetail(
        Guid accountId,
        int gameId,
        CancellationToken ct = default
    )
    {
        var detail = await matches.GetSummaryAsync(gameId, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        var items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
        return Partial("_MatchDetail", new MatchDetailView(detail, heroes, items));
    }

    public async Task<IActionResult> OnPostReindex(Guid accountId, CancellationToken ct = default)
    {
        var result = await queue.RequestReindexAsync(accountId, ct);
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        return Partial("_ReindexButton", new ReindexButtonView(accountId, result, indexed));
    }

    public IActionResult OnGetIndexProgress(Guid accountId)
    {
        var p = progressTracker.GetProgress(accountId);
        return Partial(
            "_IndexProgress",
            new IndexProgressView(accountId, p.Fetched, p.Total, p.Done, p.StartedAt)
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
}

public record MatchesView(
    Guid AccountId,
    IReadOnlyList<PlayerMatch> Matches,
    Dictionary<int, Hero> Heroes,
    int Offset,
    int PageSize,
    bool HasMore
);

public record MatchDetailView(
    MatchDetail? Detail,
    Dictionary<int, Hero> Heroes,
    Dictionary<int, Item> Items
);

public record ReindexButtonView(Guid AccountId, ReindexResult? Result, IndexedPlayer? Indexed);

public record IndexProgressView(
    Guid AccountId,
    int Fetched,
    int Total,
    bool Done,
    DateTimeOffset? StartedAt
);
