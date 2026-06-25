using HonStats.App.Players;
using HonStats.Domain.Players;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class SearchModel(IPlayerSearch search) : PageModel
{
    public string? Query { get; set; }
    public IReadOnlyList<PlayerSearchResult> Results { get; set; } = [];

    public async Task<IActionResult> OnGet(string q, CancellationToken ct)
    {
        if (Request.IsHtmx())
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Content(string.Empty);
            }

            var results = await search.SearchAsync(q, ct);
            return Partial("_SearchResults", (IReadOnlyList<PlayerSearchResult>)results);
        }

        Query = q;
        if (!string.IsNullOrWhiteSpace(q))
        {
            Results = await search.SearchAsync(q, ct);
        }

        return Page();
    }
}
