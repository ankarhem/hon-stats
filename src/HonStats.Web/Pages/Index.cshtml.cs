using HonStats.App.Leaderboard;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class IndexModel(IMatchCountQuery matchCounts) : PageModel
{
    public MatchCounts? Counts { get; set; }

    public async Task OnGet(CancellationToken ct)
    {
        Counts = await matchCounts.GetAsync(ct);
    }

    // Compact, weighty readout for the hero's scale signal: 12.3M / 840k / 1.2k / 999.
    public static string FormatCompact(int value)
    {
        return value switch
        {
            >= 1_000_000 => (value / 1_000_000.0).ToString("0.0") + "M",
            >= 10_000 => (value / 1_000.0).ToString("0") + "k",
            >= 1_000 => (value / 1_000.0).ToString("0.0") + "k",
            _ => value.ToString("N0"),
        };
    }
}
