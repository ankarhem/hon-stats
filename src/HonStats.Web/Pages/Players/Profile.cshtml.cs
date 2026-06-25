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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class ProfileModel(
    IPlayerProfileQuery profiles,
    IMatchQuery matches,
    IPlayerInsightsQuery insights,
    IReferenceDataQuery reference,
    IReindexQueue queue
) : PageModel
{
    public const int PageSize = 25;

    public Guid AccountId { get; set; }
    public PlayerProfile? Profile { get; set; }
    public IndexedPlayer? Indexed { get; set; }
    public Dictionary<int, Hero> Heroes { get; set; } = new();

    public async Task<IActionResult> OnGet(Guid accountId, CancellationToken ct)
    {
        this.AccountId = accountId;
        var profileTask = profiles.GetAsync(accountId, ct);
        var indexedTask = insights.GetIndexedPlayerAsync(accountId, ct);
        var heroesTask = reference.GetHeroesAsync(ct);
        await Task.WhenAll(profileTask, indexedTask, heroesTask);
        this.Profile = profileTask.Result;
        this.Indexed = indexedTask.Result;
        this.Heroes = heroesTask.Result.ToDictionary(h => h.Id);
        return this.Page();
    }

    public async Task<IActionResult> OnGetMatches(Guid accountId, int offset, CancellationToken ct)
    {
        var recent = await matches.GetRecentForPlayerAsync(accountId, PageSize, offset, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        return this.Partial(
            "_MatchesRegion",
            new MatchesView(accountId, recent, heroes, offset, PageSize, recent.Count == PageSize)
        );
    }

    public async Task<IActionResult> OnGetMatchesMore(
        Guid accountId,
        int offset,
        CancellationToken ct
    )
    {
        var recent = await matches.GetRecentForPlayerAsync(accountId, PageSize, offset, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        return this.Partial(
            "_MatchesRows",
            new MatchesView(accountId, recent, heroes, offset, PageSize, recent.Count == PageSize)
        );
    }

    public async Task<IActionResult> OnGetMatchDetail(
        Guid accountId,
        int gameId,
        CancellationToken ct
    )
    {
        var detail = await matches.GetSummaryAsync(gameId, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        var items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
        return this.Partial("_MatchDetail", new MatchDetailView(detail, heroes, items));
    }

    public async Task<IActionResult> OnGetTeammates(Guid accountId, CancellationToken ct)
    {
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        var teammates = await insights.GetTeammatesAsync(accountId, ct);
        return this.Partial("_TeammatesRegion", new TeammatesView(accountId, indexed, teammates));
    }

    public async Task<IActionResult> OnGetHeroBuilds(
        Guid accountId,
        int heroId,
        CancellationToken ct
    )
    {
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        var heroGames = await insights.GetHeroGamesAsync(accountId, ct);
        var heroes = (await reference.GetHeroesAsync(ct)).ToDictionary(h => h.Id);
        var items = (await reference.GetItemsAsync(ct)).ToDictionary(i => i.Id);
        var selected =
            heroId == 0 && heroGames.Count > 0
                ? heroGames.OrderByDescending(kv => kv.Value).First().Key
                : heroId;
        var build =
            selected == 0
                ? new List<HeroBuildEntry>()
                : await insights.GetHeroBuildAsync(accountId, selected, ct);
        return this.Partial(
            "_HeroBuildsRegion",
            new HeroBuildsView(accountId, indexed, heroGames, heroes, items, selected, build)
        );
    }

    public async Task<IActionResult> OnPostReindex(Guid accountId, CancellationToken ct)
    {
        var result = await queue.RequestReindexAsync(accountId, ct);
        var indexed = await insights.GetIndexedPlayerAsync(accountId, ct);
        return this.Partial("_ReindexButton", new ReindexButtonView(accountId, result, indexed));
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

public record MatchRowView(Guid AccountId, PlayerMatch Match, Dictionary<int, Hero> Heroes);

public record MatchDetailView(
    MatchDetail? Detail,
    Dictionary<int, Hero> Heroes,
    Dictionary<int, Item> Items
);

public record TeammatesView(
    Guid AccountId,
    IndexedPlayer? Indexed,
    IReadOnlyList<TeammateStat> Teammates
);

public record HeroBuildsView(
    Guid AccountId,
    IndexedPlayer? Indexed,
    IReadOnlyDictionary<int, int> HeroGames,
    Dictionary<int, Hero> Heroes,
    Dictionary<int, Item> Items,
    int HeroId,
    IReadOnlyList<HeroBuildEntry> Build
);

public record ReindexButtonView(Guid AccountId, ReindexResult? Result, IndexedPlayer? Indexed);

public record ProfileTabsView(Guid AccountId, string Active);
