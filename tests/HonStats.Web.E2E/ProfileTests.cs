using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class ProfileTests
{
    private readonly E2EFixture _e2e;

    public ProfileTests(E2EFixture e2e) => _e2e = e2e;

    private IPage Page => _e2e.Page;

    [Fact]
    public async Task Escape_key_closes_the_match_modal()
    {
        await _e2e.OpenMatchModalAsync(TestPlayers.ManyGames);

        await Page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Clicking_outside_closes_the_match_modal()
    {
        await _e2e.OpenMatchModalAsync(TestPlayers.ManyGames);

        // The modal is centered; (5, 5) is on the ::backdrop. closedby="any"
        // makes a backdrop click dismiss the dialog (Chrome/Firefox).
        await Page.Mouse.ClickAsync(5, 5);

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Close_button_closes_the_match_modal()
    {
        await _e2e.OpenMatchModalAsync(TestPlayers.ManyGames);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Close ✕" }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Dialog)).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Reindexing_populates_the_teammates_tab()
    {
        await _e2e.GotoProfileAsync(TestPlayers.FewGames);

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
        await _e2e.GotoProfileAsync(TestPlayers.ManyGames);
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
        await _e2e.GotoProfileAsync(TestPlayers.ManyGames);

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
        await _e2e.GotoProfileAsync(TestPlayers.ManyGames);

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

    [Fact]
    public async Task Map_filter_restricts_match_rows_to_the_selected_map()
    {
        await _e2e.GotoProfileAsync(TestPlayers.ManyGames);
        var mapCells = Page.Locator("[data-testid=match-row] td:nth-child(5)");
        await Assertions.Expect(mapCells.First).ToBeVisibleAsync();

        var presentMaps = (await mapCells.AllTextContentsAsync())
            .Distinct()
            .Where(m => m is "ForestsOfCaldavar" or "MidWars")
            .ToList();
        Assert.NotEmpty(presentMaps);

        foreach (var map in presentMaps)
        {
            await Page.GetByRole(AriaRole.Link, new() { Name = MapPillLabel(map) }).ClickAsync();
            await Page.WaitForHtmxEventAsync("afterSettle");

            var filtered = await mapCells.AllTextContentsAsync();
            Assert.NotEmpty(filtered);
            Assert.All(filtered, cell => Assert.Equal(map, cell));
        }
    }

    [Fact]
    public async Task Switching_tabs_renders_the_matching_region()
    {
        await _e2e.GotoProfileAsync(TestPlayers.ManyGames);
        await Assertions.Expect(Page.GetByTestId("match-row").First).ToBeVisibleAsync();

        // Fresh DB → player is unindexed, so Teammates / Hero Builds show their
        // empty-state rather than data; that still proves the region swapped.
        await ClickTabAndSettle("Teammates");
        await Assertions
            .Expect(Page.GetByText("Index this player to see who they usually play with."))
            .ToBeVisibleAsync();

        await ClickTabAndSettle("Hero Builds");
        await Assertions
            .Expect(Page.GetByText("Index this player to see what they buy on each hero."))
            .ToBeVisibleAsync();

        await ClickTabAndSettle("Matches");
        await Assertions.Expect(Page.GetByTestId("match-row").First).ToBeVisibleAsync();
    }

    private async Task ClickTabAndSettle(string name)
    {
        await Page.GetByRole(AriaRole.Link, new() { Name = name }).ClickAsync();
        await Page.WaitForHtmxEventAsync("afterSettle");
    }

    private static string MapPillLabel(string mapValue) =>
        mapValue switch
        {
            "ForestsOfCaldavar" => "Forests of Caldavar",
            "MidWars" => "Mid Wars",
            _ => throw new ArgumentOutOfRangeException(
                nameof(mapValue),
                mapValue,
                "unexpected map value"
            ),
        };

    private Task AssertNavigationKeepsScroll(ILocator link, string expectedQuery) =>
        _e2e.AssertNavigationKeepsScroll(async () =>
        {
            await link.ClickAsync();
            await Assertions
                .Expect(Page)
                .ToHaveURLAsync($"{_e2e.BaseUrl}/players/{TestPlayers.ManyGames}{expectedQuery}");
            await Assertions.Expect(link).ToHaveAttributeAsync("aria-current", "true");
        });
}
