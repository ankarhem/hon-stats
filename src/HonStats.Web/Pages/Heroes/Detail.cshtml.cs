using HonStats.App.ReferenceData;
using HonStats.Domain.ReferenceData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class HeroDetailModel(IReferenceDataQuery reference) : PageModel
{
    public Hero? Hero { get; set; }

    public async Task<IActionResult> OnGet(string slug, CancellationToken ct)
    {
        var heroes = await reference.GetHeroesAsync(ct);
        Hero = heroes.FirstOrDefault(h => h.Slug == slug);
        return Hero is null ? NotFound() : Page();
    }
}
