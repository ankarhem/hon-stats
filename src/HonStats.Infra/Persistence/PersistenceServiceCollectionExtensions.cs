using HonStats.App.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HonStats.Infra.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string DefaultConnectionString = "Data Source=honstats.db";
    public const string SectionName = "HonStats:Persistence";

    public static IServiceCollection AddHonStatsPersistence(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetSection(SectionName)["ConnectionString"] ?? DefaultConnectionString;

        services.AddDbContextFactory<HonStatsDbContext>(options =>
            options.UseSqlite(connectionString)
        );

        services.AddScoped<IMmrHistoryQuery, SqliteMmrHistoryQuery>();

        return services;
    }
}
