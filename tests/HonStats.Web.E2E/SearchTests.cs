using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class SearchTests
{
    // The Enter test only redirects to the profile on an exact (Score 100)
    // username match — a fuzzy query lands on the /search results page instead.
    private const string KnownPlayer = TestPlayers.FewGames;

    private readonly E2EFixture _e2e;

    public SearchTests(E2EFixture e2e) => _e2e = e2e;

    private IPage Page => _e2e.Page;

    private ILocator SearchBox => Page.GetByPlaceholder("Search player name…");

    [Fact]
    public async Task Typing_returns_typeahead_results()
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/");

        await SearchBox.PressSequentiallyAsync(KnownPlayer);

        await Assertions
            .Expect(Page.GetByRole(AriaRole.Link, new() { Name = KnownPlayer }).First)
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Clicking_a_result_navigates_to_the_profile()
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/");

        await SearchBox.PressSequentiallyAsync(KnownPlayer);
        var result = Page.GetByRole(AriaRole.Link, new() { Name = KnownPlayer }).First;
        await Assertions.Expect(result).ToBeVisibleAsync();

        await result.ClickAsync();

        await Assertions.Expect(Page).ToHaveURLAsync(_e2e.BaseUrl + "/players/" + KnownPlayer);
        await Assertions
            .Expect(Page.GetByRole(AriaRole.Heading, new() { Name = KnownPlayer }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Pressing_enter_navigates_to_the_first_exact_match_profile()
    {
        await Page.GotoAsync(_e2e.BaseUrl + "/");

        await SearchBox.PressSequentiallyAsync(KnownPlayer);
        await SearchBox.PressAsync("Enter");

        await Assertions.Expect(Page).ToHaveURLAsync(_e2e.BaseUrl + "/players/" + KnownPlayer);
        await Assertions
            .Expect(Page.GetByRole(AriaRole.Heading, new() { Name = KnownPlayer }))
            .ToBeVisibleAsync();
    }
}
