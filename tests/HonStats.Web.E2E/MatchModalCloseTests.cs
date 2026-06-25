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

        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Clicking_outside_closes_the_match_modal()
    {
        var page = await OpenMatchModalAsync();

        // The modal is centered; (5, 5) is on the ::backdrop. closedby="any"
        // makes a backdrop click dismiss the dialog (Chrome/Firefox).
        await page.Mouse.ClickAsync(5, 5);

        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Close_button_closes_the_match_modal()
    {
        var page = await OpenMatchModalAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Close ✕" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    private async Task<IPage> OpenMatchModalAsync()
    {
        var page = _e2e.Page;

        await page.GotoAsync(_e2e.BaseUrl + "/");
        await page.GetByPlaceholder("Search player name…").PressSequentiallyAsync(KnownPlayer);
        await page.GetByRole(AriaRole.Link, new() { Name = KnownPlayer }).ClickAsync();

        // Matches are fetched live from juvio on profile load (no indexing wait),
        // but allow for upstream latency before the rows render.
        var firstMatch = page.GetByTestId("match-row").First;
        await Assertions.Expect(firstMatch).ToBeVisibleAsync(new() { Timeout = 60_000 });

        await firstMatch.ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

        return page;
    }
}
