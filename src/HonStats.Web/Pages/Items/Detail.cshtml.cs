using HonStats.App.ReferenceData;
using HonStats.Domain.ReferenceData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class ItemDetailModel(IReferenceDataQuery reference) : PageModel
{
    public Item? Item { get; set; }

    public async Task<IActionResult> OnGet(string slug, CancellationToken ct)
    {
        var items = await reference.GetItemsAsync(ct);
        Item = items.FirstOrDefault(i => i.Slug == slug);
        return Item is null ? NotFound() : Page();
    }
}
