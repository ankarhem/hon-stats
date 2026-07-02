using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class HeroesTests
{
    private readonly E2EFixture _e2e;

    public HeroesTests(E2EFixture e2e) => _e2e = e2e;

    private IPage Page => _e2e.Page;

    [Fact]
    public async Task Name_filter_dims_non_matching_heroes()
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/heroes");
        var searchBox = Page.GetByPlaceholder("Search hero name…");
        var shown = Page.Locator(".hero-tile:not(.hero-tile--dimmed)");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();

        await searchBox.FillAsync("zzzzzzzz");
        await Assertions.Expect(shown).ToHaveCountAsync(0);

        await searchBox.FillAsync("");
        await Assertions.Expect(shown.First).ToBeVisibleAsync();
    }
}
