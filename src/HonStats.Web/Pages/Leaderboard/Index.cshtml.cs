using HonStats.App.Leaderboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class LeaderboardModel(ILeaderboardQuery leaderboard) : PageModel
{
    private const string DefaultMap = "ForestsOfCaldavar";
    private static readonly HashSet<string> ValidMaps = ["ForestsOfCaldavar", "MidWars"];

    public string Map { get; private set; } = DefaultMap;
    public IReadOnlyList<LeaderboardEntry> Entries { get; private set; } = [];

    public async Task<IActionResult> OnGet(string? map, CancellationToken ct)
    {
        this.Map = NormalizeMap(map);
        this.Entries = await leaderboard.GetAsync(this.Map, ct);
        return Page();
    }

    public static string MapLabel(string map) =>
        map switch
        {
            "ForestsOfCaldavar" => "Forests of Caldavar",
            "MidWars" => "Mid Wars",
            _ => map,
        };

    public static string Initial(string? name) =>
        string.IsNullOrEmpty(name) ? "?" : name[..1].ToUpperInvariant();

    private static string NormalizeMap(string? map) =>
        !string.IsNullOrEmpty(map) && ValidMaps.Contains(map) ? map : DefaultMap;
}
