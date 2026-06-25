using HonStats.App.Players;
using HonStats.Domain.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class SearchModel(IPlayerSearch search) : PageModel
{
    public async Task<IActionResult> OnGet(string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Content(string.Empty);
        }

        var results = await search.SearchAsync(q, ct);
        return Partial("_SearchResults", (IReadOnlyList<PlayerSearchResult>)results);
    }
}
