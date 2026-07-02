namespace HonStats.Web;

public static class BrowserCacheMiddleware
{
    // Browser cache headers so the htmx preload extension works: the preloaded
    // GET is cached, making the actual click instant. Vary by HX-Request because
    // many Razor Pages branch on Request.IsHtmx() (full page vs fragment) — without
    // it a cached fragment could be served for a full-page request.
    public static IApplicationBuilder UseBrowserCaching(this WebApplication app) =>
        app.Use(
            async (context, next) =>
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.Vary = "HX-Request";

                    if (context.Request.Method is "GET" && context.Response.StatusCode is 200)
                    {
                        var path = context.Request.Path;
                        context.Response.Headers.CacheControl = path switch
                        {
                            _ when path.StartsWithSegments("/heroes")
                                    || path.StartsWithSegments("/items") => "max-age=3600",
                            _ when path.StartsWithSegments("/search") => "no-store",
                            _ => "no-cache",
                        };
                    }

                    return Task.CompletedTask;
                });

                await next();
            }
        );
}
