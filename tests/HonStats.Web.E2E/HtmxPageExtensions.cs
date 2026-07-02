using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

// Deterministic HTMX settle-gate: call InstallHtmxSupportAsync BEFORE the
// action that triggers a swap, then WaitForHtmxSettledAsync after it.
// Server-rendered markers (aria-current etc.) can appear before htmx finishes
// settling — and also pass on an accidental full-page navigation.
internal static class HtmxPageExtensions
{
    public static Task InstallHtmxSupportAsync(this IPage page) =>
        page.EvaluateAsync(
            """
            () => {
              window.__htmxSettled = false;
              if (!window.__htmxSettleListener) {
                window.__htmxSettleListener = true;
                window.addEventListener('htmx:afterSettle', () => { window.__htmxSettled = true; });
              }
            }
            """
        );

    public static async Task WaitForHtmxSettledAsync(this IPage page)
    {
        try
        {
            await page.WaitForFunctionAsync("() => window.__htmxSettled === true");
        }
        catch (TimeoutException)
        {
            Assert.Fail("htmx did not settle (full page load or no swap?)");
        }
    }
}
