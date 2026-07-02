using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class HeroesTests
{
    private readonly E2EFixture _e2e;

    public HeroesTests(E2EFixture e2e) => _e2e = e2e;

    [Fact]
    public async Task Name_filter_dims_non_matching_heroes()
    {
        var page = _e2e.Page;
        await page.GotoAsync(_e2e.BaseUrl + "/heroes");

        // A hero tile that is neither hidden nor dimmed.
        var shown = page.Locator(".hero-tile:not(.hero-tile--dimmed)");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();

        // A query matching no hero name → every tile gets --dimmed on the swap.
        await page.GetByPlaceholder("Search hero name…").FillAsync("zzzzzzzz");
        await Assertions.Expect(shown).ToHaveCountAsync(0);

        // Clearing the query restores the undimmed grid.
        await page.GetByPlaceholder("Search hero name…").FillAsync("");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();
    }
}
