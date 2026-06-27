using HonStats.App.Events;
using HonStats.App.Players;
using HonStats.Domain.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HonStats.Infra.PlayerSearch;

public static class PlayerSearchServiceCollectionExtensions
{
    public static IServiceCollection AddPlayerNameSearch(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<PlayerNameSearchOptions>(
            configuration.GetSection(PlayerNameSearchOptions.SectionName)
        );

        // Stateless; depends only on the singleton IDbContextFactory — singleton lifetime
        // is correct and avoids a captive-dependency in the singleton seed service.
        services.AddSingleton<IPlayerCorpusQuery, SqlitePlayerCorpusQuery>();
        services.AddSingleton<PlayerNameIndexCache>();
        services.AddSingleton<IPlayerNameSearch, InMemoryPlayerNameSearch>();
        services.AddHostedService<PlayerNameIndexSeedService>();
        // Scoped like the existing rebuild handlers; resolved per-dispatch scope by the
        // DomainEventDispatcher.
        services.AddScoped<IEventHandler<PlayerMatchesIndexed>, PlayerNameIndexRefreshHandler>();

        return services;
    }
}
