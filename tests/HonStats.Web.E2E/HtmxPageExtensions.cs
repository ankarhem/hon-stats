using Microsoft.Playwright;
using Xunit;

namespace HonStats.Web.E2E;

// Call WaitForHtmxEventAsync right AFTER the action that triggers the swap.
// For network-backed swaps the event fires after the round-trip, so a listener
// registered at call time still catches it; if a no-network swap ever races it,
// revert to pre-arming a flag before the action.
internal static class HtmxPageExtensions
{
    public static async Task WaitForHtmxEventAsync(this IPage page, string eventName)
    {
        try
        {
            await page.EvaluateAsync(
                """
                (e) => Promise.race([
                  new Promise(r => window.addEventListener('htmx:' + e, () => r(), { once: true })),
                  new Promise((_, rej) => setTimeout(() => rej('timeout'), 10000))
                ])
                """,
                eventName
            );
        }
        catch (PlaywrightException)
        {
            Assert.Fail($"htmx:{eventName} did not fire (full page load or no swap?)");
        }
    }
}
