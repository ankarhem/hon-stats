using HonStats.App.ReferenceData;
using HonStats.Domain.ReferenceData;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HonStats.Web.Pages;

public class ItemsModel(IReferenceDataQuery reference) : PageModel
{
    public IReadOnlyList<Item> Items { get; set; } = [];

    public async Task OnGet(CancellationToken ct) =>
        Items = (await reference.GetItemsAsync(ct)).OrderBy(i => i.TranslatedName).ToList();
}
