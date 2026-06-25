using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class FlowTests
{
    private readonly E2EFixture _e2e;

    public FlowTests(E2EFixture e2e) => _e2e = e2e;

    [Fact]
    public async Task Search_profile_reindex_and_teammates()
    {
        var page = _e2e.Page;

        await page.GotoAsync(_e2e.BaseUrl + "/");

        await page.Locator(".search__input").PressSequentiallyAsync("idealpink");
        await page.Locator(".search-results__item a").First.ClickAsync();

        await Assertions
            .Expect(page.Locator(".profile-header__name"))
            .ToContainTextAsync("idealpink");
        await Assertions.Expect(page.GetByText("Wards placed")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Reindex" }).ClickAsync();

        // The tab content is static once loaded, so re-open Teammates to re-fetch
        // as background indexing completes; the roster renders once it finishes.
        var deadline = DateTime.UtcNow.AddSeconds(240);
        while (DateTime.UtcNow < deadline)
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Teammates" }).ClickAsync();
            try
            {
                await Assertions
                    .Expect(page.Locator(".roster__item").First)
                    .ToBeVisibleAsync(new() { Timeout = 5000 });
                return;
            }
            catch (PlaywrightException) { }
            await page.WaitForTimeoutAsync(5000);
        }

        throw new TimeoutException("Teammates did not populate within 240s (indexing)");
    }
}
