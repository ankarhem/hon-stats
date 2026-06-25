using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class MatchModalCloseTests
{
    private const string KnownPlayer = "idealpink";

    private readonly E2EFixture _e2e;

    public MatchModalCloseTests(E2EFixture e2e) => _e2e = e2e;

    [Fact]
    public async Task Escape_key_closes_the_match_modal()
    {
        var page = await OpenMatchModalAsync();

        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.Locator("dialog.match-modal")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Clicking_outside_closes_the_match_modal()
    {
        var page = await OpenMatchModalAsync();

        // The modal is centered; (5, 5) is on the ::backdrop. closedby="any"
        // makes a backdrop click dismiss the dialog (Chrome/Firefox).
        await page.Mouse.ClickAsync(5, 5);

        await Assertions.Expect(page.Locator("dialog.match-modal")).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Close_button_closes_the_match_modal()
    {
        var page = await OpenMatchModalAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Close ✕" }).ClickAsync();

        await Assertions.Expect(page.Locator("dialog.match-modal")).ToBeHiddenAsync();
    }

    private async Task<IPage> OpenMatchModalAsync()
    {
        var page = _e2e.Page;

        await page.GotoAsync(_e2e.BaseUrl + "/");
        await page.Locator(".search__input").PressSequentiallyAsync(KnownPlayer);
        await page.Locator(".search-results__item a").First.ClickAsync();

        // Matches are fetched live from juvio on profile load (no indexing wait),
        // but allow for upstream latency before the rows render.
        await Assertions
            .Expect(page.Locator(".match-row").First)
            .ToBeVisibleAsync(new() { Timeout = 60_000 });

        await page.Locator(".match-row").First.ClickAsync();

        await Assertions.Expect(page.Locator("dialog.match-modal")).ToBeVisibleAsync();

        return page;
    }
}
