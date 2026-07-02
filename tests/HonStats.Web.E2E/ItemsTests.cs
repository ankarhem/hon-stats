using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class ItemsTests
{
    private readonly E2EFixture _e2e;

    public ItemsTests(E2EFixture e2e) => _e2e = e2e;

    [Fact]
    public async Task Name_filter_dims_non_matching_items()
    {
        var page = _e2e.Page;
        await page.GotoAsync(_e2e.BaseUrl + "/items");

        var shown = page.Locator(".item-tile:not(.item-tile--dimmed)");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();

        await page.GetByPlaceholder("Search item name…").FillAsync("zzzzzzzz");
        await Assertions.Expect(shown).ToHaveCountAsync(0);

        await page.GetByPlaceholder("Search item name…").FillAsync("");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();
    }
}
