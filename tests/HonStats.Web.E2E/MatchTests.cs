using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class MatchTests
{
    private readonly E2EFixture _e2e;

    public MatchTests(E2EFixture e2e) => _e2e = e2e;

    private IPage Page => _e2e.Page;

    private async Task<string> OpenModalAsync()
    {
        await _e2e.GotoProfileAsync(TestPlayers.ManyGames);
        await Page.GetByTestId("match-row")
            .Filter(new() { HasText = "ForestsOfCaldavar" })
            .First.ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        var href = await Page.Locator("dialog.modal[open] a[href^='/match/']")
            .GetAttributeAsync("href");
        return href!["/match/".Length..];
    }

    [Fact]
    public async Task Modal_shows_per_minute_rates_and_role_chip()
    {
        await OpenModalAsync();
        var dialog = Page.GetByRole(AriaRole.Dialog);

        await Assertions.Expect(dialog.GetByText("GPM").First).ToBeVisibleAsync();
        await Assertions.Expect(dialog.GetByText("XPM").First).ToBeVisibleAsync();
        await Assertions.Expect(dialog.GetByText("DPM").First).ToBeVisibleAsync();
        await Assertions
            .Expect(Page.Locator("dialog.modal img.hero-icon__role").First)
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Modal_link_navigates_to_full_match_page()
    {
        await OpenModalAsync();
        await Page.Locator("dialog.modal[open] a[href^='/match/']").ClickAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(new Regex(@"/match/\d+$"));
        await Assertions.Expect(Page.GetByText("Net Worth Over Time")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Match_page_renders_charts_and_build_timelines()
    {
        var gameId = await OpenModalAsync();
        await Page.GotoAsync(_e2e.BaseUrl + "/match/" + gameId);

        await Assertions.Expect(Page.Locator("svg.match-chart")).ToHaveCountAsync(2);
        await Page.Locator(".match-build__summary").First.ClickAsync();
        await Assertions.Expect(Page.Locator(".build-timeline").First).ToBeVisibleAsync();
        await Assertions.Expect(Page.Locator(".skill-build").First).ToBeVisibleAsync();
    }
}
