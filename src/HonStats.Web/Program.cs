using HonStats.Infra.Insights;
using HonStats.Infra.Juvio;
using HonStats.Infra.Persistence;
using HonStats.Infra.ReferenceData;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddJuvioClients(builder.Configuration);
builder.Services.AddHonStatsPersistence(builder.Configuration);
builder.Services.AddReferenceData(builder.Configuration);
builder.Services.AddInsights(builder.Configuration);

var app = builder.Build();

await MigrateDatabaseAsync(app.Services);

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
return;

static async Task MigrateDatabaseAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<HonStatsDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}
