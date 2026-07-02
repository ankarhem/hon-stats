using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class ItemsTests
{
    private readonly E2EFixture _e2e;

    public ItemsTests(E2EFixture e2e) => _e2e = e2e;

    private IPage Page => _e2e.Page;

    [Fact]
    public async Task Name_filter_dims_non_matching_items()
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/items");
        var searchBox = Page.GetByPlaceholder("Search item name…");
        var shown = Page.Locator(".item-tile:not(.item-tile--dimmed)");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();

        await searchBox.FillAsync("zzzzzzzz");
        await Assertions.Expect(shown).ToHaveCountAsync(0);

        await searchBox.FillAsync("");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();
    }
}
