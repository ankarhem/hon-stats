using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class FlowTests
{
    private const string KnownPlayer = "Testie";

    private readonly E2EFixture _e2e;

    public FlowTests(E2EFixture e2e) => _e2e = e2e;

    [Fact]
    public async Task Search_profile_reindex_and_teammates()
    {
        var page = _e2e.Page;

        await page.GotoAsync(_e2e.BaseUrl + "/");

        await page.GetByPlaceholder("Search player name…").PressSequentiallyAsync(KnownPlayer);
        await page.GetByRole(AriaRole.Link, new() { Name = KnownPlayer }).ClickAsync();

        await Assertions
            .Expect(page.GetByRole(AriaRole.Heading, new() { Name = KnownPlayer }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Avg wards")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Index" })
            .Or(page.GetByRole(AriaRole.Button, new() { Name = "Reindex" }))
            .ClickAsync();

        // The tab content is static once loaded, so re-open Teammates to re-fetch
        // as background indexing completes; the roster renders once it finishes.
        var teammatesTab = page.GetByRole(AriaRole.Link, new() { Name = "Teammates" });
        var firstTeammate = page.GetByTestId("teammate-row").First;
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
}
