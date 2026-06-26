using HonStats.App.Matches;
using HonStats.App.Players;
using HonStats.Infra.Juvio.Adapters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace HonStats.Infra.Juvio;

public static class JuvioServiceCollectionExtensions
{
    public static IServiceCollection AddJuvioClients(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .Configure<JuvioOptions>(configuration.GetSection(JuvioOptions.SectionName))
            .AddSingleton<ITokenPool, TokenPool>()
            .AddTransient<AuthenticatingHandler>();

        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500))
            );

        // Internal client used to fetch auth tokens — deliberately has no
        // AuthenticatingHandler to avoid the bootstrapping cycle.
        services
            .AddHttpClient(
                JuvioHttpClients.Auth,
                (sp, client) => client.BaseAddress = new Uri(sp.GetJuvioOptions().AuthBaseUrl)
            )
            .AddPolicyHandler(retry);

        // Gamedata endpoints are public (no bearer needed).
        services
            .AddHttpClient(
                JuvioHttpClients.GameData,
                (sp, client) => client.BaseAddress = new Uri(sp.GetJuvioOptions().GameDataBaseUrl)
            )
            .AddPolicyHandler(retry);

        services
            .AddHttpClient(
                JuvioHttpClients.Stats,
                (sp, client) => client.BaseAddress = new Uri(sp.GetJuvioOptions().StatsBaseUrl)
            )
            .AddPolicyHandler(retry)
            .AddHttpMessageHandler<AuthenticatingHandler>();

        services.AddMemoryCache();

        services.AddScoped<JuvioPlayerNameResolver>();
        services.AddScoped<IPlayerNameResolver>(sp => new CachedPlayerNameResolver(
            sp.GetRequiredService<JuvioPlayerNameResolver>(),
            sp.GetRequiredService<IMemoryCache>()
        ));

        services.AddScoped<IPlayerSearch>(sp => new CachedPlayerSearch(
            ActivatorUtilities.CreateInstance<JuvioPlayerSearch>(sp),
            sp.GetRequiredService<IMemoryCache>()
        ));
        services.AddScoped<IPlayerProfileQuery, JuvioPlayerProfileQuery>();
        services.AddScoped<IMatchQuery, JuvioMatchQuery>();

        return services;
    }

    private static JuvioOptions GetJuvioOptions(this IServiceProvider sp) =>
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JuvioOptions>>().Value;
}
