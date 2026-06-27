using HonStats.App.Players;
using Microsoft.Extensions.DependencyInjection;

namespace HonStats.Infra.Players;

public static class PlayersServiceCollectionExtensions
{
    public static IServiceCollection AddHonStatsPlayers(this IServiceCollection services)
    {
        services.AddSingleton<IPlayerNameStore, SqlitePlayerNameStore>();

        return services;
    }
}
