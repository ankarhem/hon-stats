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
        await Page.GotoAsync(_e2e.BaseUrl + "/players/" + IndexPlayer);

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
                await Assertions.Expect(firstTeammate).ToBeVisibleAsync(new() { Timeout = 5000 });
                return;
            }
            catch (PlaywrightException) { }
        }

        throw new TimeoutException("Teammates did not populate within 240s (indexing)");
    }

    private async Task OpenMatchModalAsync()
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/players/" + ModalPlayer);

        var firstMatch = Page.GetByTestId("match-row").First;
        await Assertions.Expect(firstMatch).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await firstMatch.ClickAsync();

        // The match-detail payload is a separate juvio fetch, so give the dialog
        // its own window rather than the default 5s (which flakes under load).
        await Assertions
            .Expect(Page.GetByRole(AriaRole.Dialog))
            .ToBeVisibleAsync(new() { Timeout = 30_000 });
    }
}
