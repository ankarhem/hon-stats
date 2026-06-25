using HonStats.App.Matches;
using HonStats.App.Players;
using HonStats.Infra.Juvio.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            .AddSingleton<ITokenProvider, AuthTokenProvider>()
            .AddTransient<AuthenticatingHandler>();

        // Internal client used to fetch the auth token itself — deliberately
        // has no AuthenticatingHandler to avoid the bootstrapping cycle.
        services.AddHttpClient(
            JuvioHttpClients.Auth,
            (sp, client) => client.BaseAddress = new Uri(sp.GetJuvioOptions().AuthBaseUrl)
        );

        services
            .AddHttpClient(
                JuvioHttpClients.GameData,
                (sp, client) => client.BaseAddress = new Uri(sp.GetJuvioOptions().GameDataBaseUrl)
            )
            .AddHttpMessageHandler<AuthenticatingHandler>();

        services
            .AddHttpClient(
                JuvioHttpClients.Stats,
                (sp, client) => client.BaseAddress = new Uri(sp.GetJuvioOptions().StatsBaseUrl)
            )
            .AddHttpMessageHandler<AuthenticatingHandler>();

        services.AddScoped<IPlayerSearch, JuvioPlayerSearch>();
        services.AddScoped<IPlayerProfileQuery, JuvioPlayerProfileQuery>();
        services.AddScoped<IMatchQuery, JuvioMatchQuery>();

        return services;
    }

    private static JuvioOptions GetJuvioOptions(this IServiceProvider sp) =>
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JuvioOptions>>().Value;
}
