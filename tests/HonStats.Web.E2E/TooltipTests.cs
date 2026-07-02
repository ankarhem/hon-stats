using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

[Collection("E2E")]
public class TooltipTests
{
    private const string KnownPlayer = "idealpink";

    private readonly E2EFixture _e2e;

    public TooltipTests(E2EFixture e2e) => _e2e = e2e;

    [Fact]
    public async Task Hero_tooltip_renders_below_the_hero_tile()
    {
        var page = _e2e.Page;
        await page.GotoAsync(_e2e.BaseUrl + "/heroes");

        await AssertTooltipAnchoredBelowTile(page.Locator(".hero-tile").First);
    }

    [Fact]
    public async Task Item_tooltip_renders_below_the_item_tile()
    {
        var page = _e2e.Page;
        await page.GotoAsync(_e2e.BaseUrl + "/items");

        await AssertTooltipAnchoredBelowTile(page.Locator(".item-tile").First);
    }

    [Fact]
    public async Task Item_tooltip_anchors_below_the_tile_inside_the_match_modal()
    {
        var page = await OpenMatchModalAsync();

        // Only tiles that carry a tooltip — empty inventory slots render none.
        var tile = page.Locator(
            "dialog.modal[open] .loadout-item.item-tile:has(.item-tooltip)"
        ).First;

        await AssertTooltipAnchoredBelowTile(tile);
    }

    // Hovering a tile must reveal its tooltip directly below it, left edge aligned
    // (position-area: bottom span-right). A broken/static tooltip would land far off,
    // failing the range checks. Asserts viewport-relative bounding boxes via Playwright
    // (no JS evaluation), so it reflects where the fixed-position panel actually rendered.
    private static async Task AssertTooltipAnchoredBelowTile(ILocator tile)
    {
        var tooltip = tile.Locator(".hero-tooltip, .item-tooltip");
        await tile.HoverAsync();
        await Assertions.Expect(tooltip).ToBeVisibleAsync();

        var tileBox =
            await tile.BoundingBoxAsync()
            ?? throw new InvalidOperationException("tile has no bounding box");
        var tipBox =
            await tooltip.BoundingBoxAsync()
            ?? throw new InvalidOperationException("tooltip has no bounding box");

        // Top edge sits just past the tile bottom (margin-block: 0.4rem ≈ 6px gap).
        var tileBottom = tileBox.Y + tileBox.Height;
        Assert.InRange(tipBox.Y, tileBottom, tileBottom + 16);
        // Left edges align (span-right, not span-left — these tiles have room to the right).
        Assert.InRange(tipBox.X, tileBox.X - 1, tileBox.X + 1);
    }

    private async Task<IPage> OpenMatchModalAsync()
    {
        var page = _e2e.Page;

        await page.GotoAsync(_e2e.BaseUrl + "/players/" + KnownPlayer);

        var firstMatch = page.GetByTestId("match-row").First;
        await Assertions.Expect(firstMatch).ToBeVisibleAsync(new() { Timeout = 60_000 });
        await firstMatch.ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();

        return page;
    }
}
