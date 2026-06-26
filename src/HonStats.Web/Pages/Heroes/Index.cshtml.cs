using HonStats.App.ReferenceData;
using HonStats.Domain.ReferenceData;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class HeroesModel(IReferenceDataQuery reference) : PageModel
{
    public IReadOnlyList<Hero> Heroes { get; set; } = [];

    public async Task OnGet(CancellationToken ct) =>
        Heroes = (await reference.GetHeroesAsync(ct)).OrderBy(h => h.TranslatedName).ToList();
}
