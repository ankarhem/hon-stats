using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HonStats.Infra.Persistence;

// Lets `dotnet ef migrations add` build a context without a running app host.
// The connection string is resolved at runtime via AddHonStatsPersistence.
public sealed class HonStatsDbContextDesignFactory : IDesignTimeDbContextFactory<HonStatsDbContext>
{
    public HonStatsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HonStatsDbContext>().UseSqlite(
            "Data Source=honstats-design.db"
        );
        return new HonStatsDbContext(options.Options);
    }
}
