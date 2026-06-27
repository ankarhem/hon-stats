using System.Text;
using HonStats.App.Events;
using HonStats.App.Indexing;
using HonStats.Domain.Events;
using HonStats.Domain.Insights;
using HonStats.Infra.Insights;
using HonStats.Infra.Juvio;
using HonStats.Infra.Persistence;
using HonStats.Infra.Players;
using HonStats.Infra.PlayerSearch;
using HonStats.Infra.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddJuvioClients(builder.Configuration);
builder.Services.AddHonStatsPersistence(builder.Configuration);
builder.Services.AddHonStatsPlayers();
builder.Services.AddReferenceData(builder.Configuration);
builder.Services.AddPlayerNameSearch(builder.Configuration);
builder.Services.AddInsights(builder.Configuration);

var app = builder.Build();

await MigrateDatabaseAsync(app.Services);

app.UseStaticFiles();
app.MapRazorPages();

app.MapPost(
    "/admin/backfill",
    async (
        HttpContext ctx,
        IDomainEventDispatcher dispatcher,
        IReindexQueue queue,
        IDbContextFactory<HonStatsDbContext> dbFactory,
        IOptions<JuvioOptions> juvioOptions,
        bool force
    ) =>
    {
        if (!IsAuthorized(ctx, juvioOptions.Value))
        {
            return Results.Unauthorized();
        }

        await using var db = await dbFactory.CreateDbContextAsync(ctx.RequestAborted);
        var accountIds = await db
            .IndexedPlayers.Where(p => p.Status == IndexingStatus.Indexed)
            .Select(p => p.AccountId)
            .ToListAsync(ctx.RequestAborted);

        if (force)
        {
            foreach (var id in accountIds)
            {
                await queue.RequestReindexAsync(id, forceBackfill: true, ctx.RequestAborted);
            }

            return Results.Ok(
                new
                {
                    players = accountIds.Count,
                    enqueued = accountIds.Count,
                    phase = "full",
                }
            );
        }

        foreach (var id in accountIds)
        {
            await dispatcher.DispatchAsync(new PlayerMatchesIndexed(id, []), ctx.RequestAborted);
        }

        return Results.Ok(
            new
            {
                players = accountIds.Count,
                dispatched = accountIds.Count,
                phase = "events",
            }
        );
    }
);

app.Run();
return;

static bool IsAuthorized(HttpContext ctx, JuvioOptions juvio)
{
    var header = ctx.Request.Headers.Authorization.ToString();
    if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    try
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..]));
        var separator = decoded.IndexOf(':');
        if (separator < 0)
        {
            return false;
        }

        var user = decoded[..separator];
        var pass = decoded[(separator + 1)..];
        return user == juvio.Username && pass == juvio.Password;
    }
    catch (FormatException)
    {
        return false;
    }
}

static async Task MigrateDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HonStatsDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();

    var stuck = await db
        .IndexedPlayers.Where(p => p.Status == IndexingStatus.Indexing)
        .ToListAsync();
    foreach (var p in stuck)
        p.Status = IndexingStatus.Failed;
    if (stuck.Count > 0)
        await db.SaveChangesAsync();
}
