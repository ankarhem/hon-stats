using System.Threading.Channels;
using HonStats.App.Events;
using HonStats.App.Indexing;
using HonStats.App.Insights;
using HonStats.App.Players;
using HonStats.Domain.Events;
using HonStats.Infra.Juvio.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HonStats.Infra.Insights;

public static class InsightsServiceCollectionExtensions
{
    public static IServiceCollection AddInsights(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<IndexingOptions>(configuration.GetSection(IndexingOptions.SectionName));

        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<IPlayerNameResolver, JuvioPlayerNameResolver>();
        services.AddScoped<IInsightsRawQuery, InsightsRawQuery>();
        services.AddScoped<IInsightsAggregateStore, InsightsAggregateStore>();
        services.AddScoped<IPlayerInsightsQuery, SqlitePlayerInsightsQuery>();
        services.AddScoped<IPlayerInsightsIndexer, JuvioPlayerInsightsIndexer>();
        services.AddScoped<IEventHandler<PlayerMatchesIndexed>, RebuildHeroBuildsHandler>();
        services.AddScoped<IEventHandler<PlayerMatchesIndexed>, RebuildTeammatesHandler>();

        services.AddSingleton(_ =>
            Channel.CreateUnbounded<ReindexRequest>(
                new UnboundedChannelOptions { SingleReader = true }
            )
        );
        services.AddScoped<IReindexQueue, ChannelReindexQueue>();
        services.AddHostedService<PlayerIndexingService>();

        return services;
    }
}
