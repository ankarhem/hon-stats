using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class ProfileTests
{
    private const string ModalPlayer = "idealpink";
    private const string IndexPlayer = "Testie";

    private readonly E2EFixture _e2e;

    public ProfileTests(E2EFixture e2e) => _e2e = e2e;

    private IPage Page => _e2e.Page;

    [Fact]
    public async Task Escape_key_closes_the_match_modal()
    {
        await OpenMatchModalAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Clicking_outside_closes_the_match_modal()
    {
        await OpenMatchModalAsync();

        // The modal is centered; (5, 5) is on the ::backdrop. closedby="any"
        // makes a backdrop click dismiss the dialog (Chrome/Firefox).
        await Page.Mouse.ClickAsync(5, 5);

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Close_button_closes_the_match_modal()
    {
        await OpenMatchModalAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Close ✕" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Reindexing_populates_the_teammates_tab()
    {
        await GotoProfileAsync(IndexPlayer);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Index" })
            .Or(Page.GetByRole(AriaRole.Button, new() { Name = "Reindex" }))
            .ClickAsync();

        // The tab content is static once loaded, so re-open Teammates to re-fetch
        // as background indexing completes; the roster renders once it finishes.
        var teammatesTab = Page.GetByRole(AriaRole.Link, new() { Name = "Teammates" });
        var firstTeammate = Page.GetByTestId("teammate-row").First;
        var deadline = DateTime.UtcNow.AddSeconds(240);
        while (DateTime.UtcNow < deadline)
        {
            await teammatesTab.ClickAsync();
            try
            {
                await Assertions.Expect(firstTeammate).ToBeVisibleAsync();
                return;
            }
            catch (PlaywrightException) { }
        }

        throw new TimeoutException("Teammates did not populate within 240s (indexing)");
    }

    [Fact]
    public async Task Match_list_loads_more_rows_on_scroll()
    {
        await GotoProfileAsync();
        var matchRows = Page.GetByTestId("match-row");
        var initial = await matchRows.CountAsync();
        var sentinel = Page.Locator(".loading-row");
        await Assertions.Expect(sentinel).ToBeAttachedAsync();

        await sentinel.ScrollIntoViewIfNeededAsync();

        await Assertions
            .Expect(matchRows.Nth(initial))
            .ToBeAttachedAsync(new() { Timeout = 15000 });
    }

    [Fact]
    public async Task Switching_tabs_does_not_scroll_back_to_top()
    {
        await GotoProfileAsync();

        await AssertNavigationKeepsScroll(
            Page.GetByRole(AriaRole.Link, new() { Name = "Teammates" }),
            "?tab=teammates&map=all"
        );
        await AssertNavigationKeepsScroll(
            Page.GetByRole(AriaRole.Link, new() { Name = "Hero Builds" }),
            "?tab=heroBuilds&map=all"
        );
        await AssertNavigationKeepsScroll(
            Page.GetByRole(AriaRole.Link, new() { Name = "Matches" }),
            "?tab=matches&map=all"
        );
    }

    [Fact]
    public async Task Changing_map_filter_does_not_scroll_back_to_top()
    {
        await GotoProfileAsync();

        await AssertNavigationKeepsScroll(
            Page.GetByRole(AriaRole.Link, new() { Name = "Mid Wars" }),
            "?map=MidWars&tab=matches"
        );
        await AssertNavigationKeepsScroll(
            Page.GetByRole(AriaRole.Link, new() { Name = "Forests of Caldavar" }),
            "?map=ForestsOfCaldavar&tab=matches"
        );
        await AssertNavigationKeepsScroll(
            Page.GetByRole(AriaRole.Link, new() { Name = "All" }),
            "?map=all&tab=matches"
        );
    }

    private async Task AssertNavigationKeepsScroll(ILocator link, string expectedQuery)
    {
        // Scroll far enough that a reset-to-top is detectable, but not so far that
        // the nav links slide under the sticky header — Playwright would then have
        // to auto-scroll to click, which moves scrollY and poisons the measurement.
        await Page.EvaluateAsync(
            "() => { document.body.style.minHeight = '3000px'; window.scrollTo(0, 200); }"
        );
        var before = await GetScrollYAsync();
        Assert.True(before > 0, $"setup failed: page did not scroll (scrollY={before})");

        await link.ClickAsync();
        await Assertions
            .Expect(Page)
            .ToHaveURLAsync(_e2e.BaseUrl + "/players/" + ModalPlayer + expectedQuery);
        await Assertions.Expect(link).ToHaveAttributeAsync("aria-current", "true");

        var after = await GetScrollYAsync();
        Assert.True(after == before, $"navigation moved scroll from {before} to {after}");
    }

    private async Task<int> GetScrollYAsync() =>
        int.Parse(await Page.EvaluateAsync<string>("() => String(Math.round(window.scrollY))"));

    private async Task GotoProfileAsync(string username = ModalPlayer)
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/players/" + username);
        await Assertions.Expect(Page.GetByTestId("match-row").First).ToBeVisibleAsync();
    }

    private async Task OpenMatchModalAsync()
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/players/" + ModalPlayer);

        var firstMatch = Page.GetByTestId("match-row").First;
        await Assertions.Expect(firstMatch).ToBeVisibleAsync();
        await firstMatch.ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
    }
}
