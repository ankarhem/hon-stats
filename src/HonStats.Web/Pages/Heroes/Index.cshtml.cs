using HonStats.App.ReferenceData;
using HonStats.App.Search;
using HonStats.Domain.ReferenceData;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class HeroesModel(IReferenceDataQuery reference) : PageModel
{
    public IReadOnlyList<Hero> Heroes { get; set; } = [];
    public string? Query { get; set; }
    public HashSet<HeroRole> ActiveRoles { get; set; } = [];

    public static readonly IReadOnlyList<(HeroRole Role, string Label)> Roles =
    [
        (HeroRole.Carry, "Carry"),
        (HeroRole.Mid, "Mid"),
        (HeroRole.Offlane, "Offlane"),
        (HeroRole.SoloOfflane, "Solo Offlane"),
        (HeroRole.SoftSupport, "Soft Support"),
        (HeroRole.HardSupport, "Hard Support"),
        (HeroRole.Jungle, "Jungle"),
    ];

    public async Task<IActionResult> OnGet(
        string? q = null,
        string[]? roles = null,
        CancellationToken ct = default
    )
    {
        Response.Headers["Vary"] = "HX-Request";
        Heroes = (await reference.GetHeroesAsync(ct)).OrderBy(h => h.TranslatedName).ToList();
        Query = q;
        ActiveRoles = (roles ?? [])
            .Select(r => Enum.TryParse<HeroRole>(r, out var role) ? role : (HeroRole?)null)
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .ToHashSet();

        return Request.IsHtmxNonBoosted() ? Partial("Shared/Partials/_HeroGrid", this) : Page();
    }

    public bool IsDimmed(Hero h)
    {
        var nameMatch = SearchMatcher.Matches(Query, h.TranslatedName);
        var roleMatch = ActiveRoles.Count == 0 || ActiveRoles.Any(h.MatchesRole);
        return !(nameMatch && roleMatch);
    }

    public IEnumerable<Hero> ByAttribute(int attribute) =>
        Heroes.Where(h => h.PrimaryAttribute == attribute);
}
