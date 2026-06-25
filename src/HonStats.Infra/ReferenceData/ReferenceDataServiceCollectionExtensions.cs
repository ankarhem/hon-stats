using HonStats.App.ReferenceData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HonStats.Infra.ReferenceData;

public static class ReferenceDataServiceCollectionExtensions
{
    public static IServiceCollection AddReferenceData(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<ReferenceDataOptions>(
            configuration.GetSection(ReferenceDataOptions.SectionName)
        );

        services.AddSingleton<GamedataClient>();
        services.AddSingleton<CachedReferenceData>();
        services.AddSingleton<IReferenceDataQuery, CachedReferenceDataQuery>();
        services.AddHostedService<ReferenceDataRefreshService>();

        return services;
    }
}
