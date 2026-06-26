using HonStats.App.ReferenceData;
using HonStats.Domain.ReferenceData;
using Htmx;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class ItemsModel(IReferenceDataQuery reference) : PageModel
{
    public IReadOnlyList<Item> Items { get; set; } = [];
    public string? Query { get; set; }

    public async Task<IActionResult> OnGet(string? q = null, CancellationToken ct = default)
    {
        Response.Headers["Vary"] = "HX-Request";
        Items = (await reference.GetItemsAsync(ct)).OrderBy(i => i.DisplayName).ToList();
        Query = q;

        return Request.IsHtmx() ? Partial("_ItemGrid", this) : Page();
    }

    public bool IsDimmed(Item i) =>
        !string.IsNullOrWhiteSpace(Query)
        && !i.DisplayName.Contains(Query, StringComparison.OrdinalIgnoreCase);
}
