using HonStats.App.ReferenceData;
using HonStats.App.Search;
using HonStats.Domain.ReferenceData;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class ItemsModel(IReferenceDataQuery reference) : PageModel
{
    public static readonly IReadOnlyList<(string Token, string Label)> Categories =
    [
        ("damage", "Damage"),
        ("armor", "Defense"),
        ("magic", "Enchantment"),
        ("utility", "Supportive"),
        ("consumable", "Consumables"),
        ("boots", "Boots"),
        ("phoenix_rewards", "Phoenix Rewards"),
    ];

    public IReadOnlyList<Item> Items { get; set; } = [];
    public string? Query { get; set; }
    public string? Category { get; set; }

    public async Task<IActionResult> OnGet(
        string? q = null,
        string? cat = null,
        CancellationToken ct = default
    )
    {
        Response.Headers["Vary"] = "HX-Request";
        var all = await reference.GetItemsAsync(ct);
        Category = cat is null
            ? null
            : Categories
                .Where(c => string.Equals(c.Token, cat, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.Token)
                .FirstOrDefault();

        IEnumerable<Item> filtered = all;
        if (Category is { } token)
            filtered = all.Where(i => i.ShopCategories.Contains(token));
        Items = filtered.OrderBy(i => i.DisplayName).ToList();
        Query = q;

        var hxTarget = Request.Headers["HX-Target"].ToString();
        return Request.IsHtmxNonBoosted()
            ? (
                hxTarget == "item-grid"
                    ? Partial("Shared/Partials/_ItemGrid", this)
                    : Partial("Shared/Partials/_ItemRegion", this)
            )
            : Page();
    }

    public bool IsDimmed(Item i) => !SearchMatcher.Matches(Query, i.DisplayName);
}
