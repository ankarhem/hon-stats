using HonStats.App.Players;
using HonStats.Domain.Players;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class SearchModel(IPlayerSearch search, IPlayerNameSearch localSearch) : PageModel
{
    private const int ResultLimit = 15;

    public string? Query { get; set; }
    public IReadOnlyList<PlayerNameSearchResult> Results { get; set; } = [];

    public async Task<IActionResult> OnGet(string q, CancellationToken ct)
    {
        Response.Headers["Vary"] = "HX-Request";

        if (Request.IsHtmxNonBoosted())
        {
            if (string.IsNullOrWhiteSpace(q))
                return Content(string.Empty);

            return Partial("_SearchResults", await BuildMergedResultsAsync(q, ct));
        }

        Query = q;
        if (!string.IsNullOrWhiteSpace(q))
        {
            Results = await BuildMergedResultsAsync(q, ct);
            // Redirect only on an exact (Score 100) hit — juvio exact or a local exact
            // match. Fuzzy-only matches fall through to the page so the visitor chooses.
            if (Results.Count > 0 && Results[0].Score == 100)
                return Redirect($"/players/{Uri.EscapeDataString(Results[0].Username)}");
        }

        return Page();
    }

    // Merges the authoritative juvio exact-match lookup (Score 100) with the local
    // fuzzy index over indexed players. Both run in parallel; results are deduped by
    // AccountId (preferring the row carrying a DisplayName) and ordered exact-first,
    // then by descending score with a stable username tiebreak. IPlayerSearch stays
    // exact-only, so Profile.cshtml.cs's username resolution is unaffected.
    private async Task<IReadOnlyList<PlayerNameSearchResult>> BuildMergedResultsAsync(
        string q,
        CancellationToken ct
    )
    {
        var juvioTask = search.SearchAsync(q, ct);
        var localTask = localSearch.SearchAsync(q, ResultLimit, ct);
        await Task.WhenAll(juvioTask, localTask);

        var juvio = await juvioTask;
        var local = await localTask;

        var merged = new List<PlayerNameSearchResult>(juvio.Count + local.Count);
        foreach (var j in juvio)
            merged.Add(
                new PlayerNameSearchResult
                {
                    AccountId = j.AccountId,
                    Username = j.Username,
                    DisplayName = null,
                    Score = 100,
                }
            );
        merged.AddRange(local);

        return merged
            .GroupBy(r => r.AccountId)
            // Within an account, prefer the row with a DisplayName (the local one), then
            // the higher score.
            .Select(g =>
                g.OrderByDescending(r => r.DisplayName is { Length: > 0 } ? 1 : 0)
                    .ThenByDescending(r => r.Score)
                    .First()
            )
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Username, StringComparer.Ordinal)
            .Take(ResultLimit)
            .ToList();
    }
}
