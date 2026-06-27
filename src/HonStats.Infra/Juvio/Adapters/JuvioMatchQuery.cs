using System.Text.Json;
using HonStats.App.Matches;
using HonStats.Domain.Matches;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioMatchQuery(IHttpClientFactory httpClientFactory) : IMatchQuery
{
    public async Task<IReadOnlyList<PlayerMatch>> GetRecentForPlayerAsync(
        Guid playerId,
        int limit,
        int offset,
        CancellationToken ct = default
    )
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(
            $"/v1/stats/getrecentmatchesforplayer?playerId={playerId}&limit={limit}&offset={offset}",
            ct
        );
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<RecentMatchesDto>(
            stream,
            JuvioJson.Options,
            ct
        );
        return (dto?.MatchesValue ?? []).Select(m => MapRecent(m, playerId)).ToList();
    }

    public async Task<MatchDetail?> GetSummaryAsync(int gameId, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(
            $"/v1/stats/getmatchsummary?gameId={gameId}",
            ct
        );
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<MatchSummaryDto>(
            stream,
            JuvioJson.Options,
            ct
        );
        return dto is null ? null : MapSummary(dto);
    }

    private static PlayerMatch MapRecent(RecentMatchDto m, Guid playerId) =>
        new()
        {
            AccountId = playerId,
            GameId = m.GameId,
            HeroId = m.HeroId,
            Team = m.Team,
            WinningTeam = m.WinningTeam,
            Kills = m.Kills,
            Deaths = m.Deaths,
            Assists = m.Assists,
            Date = m.Date,
            Duration = m.Duration,
            Map = m.Map,
            IsArranged = m.IsArranged,
        };

    private static MatchDetail MapSummary(MatchSummaryDto dto) =>
        new()
        {
            GameId = dto.GameId,
            Date = dto.Date,
            Duration = dto.Duration,
            WinningTeam = dto.WinningTeam,
            Map = dto.Map,
            IsArranged = dto.IsArranged,
            Players = dto.PlayersValue.Select(MapPlayer).ToList(),
        };

    private static MatchPlayer MapPlayer(MatchPlayerSummaryDto p) =>
        new()
        {
            AccountId = Guid.Parse(p.AccountId),
            Team = p.Team,
            HeroId = p.HeroId,
            Kills = p.Kills,
            Deaths = p.Deaths,
            Assists = p.Assists,
            CreepKills = p.CreepKills,
            NeutralKills = p.NeutralKills,
            CreepDenies = p.CreepDenies,
            NetWorth = p.NetWorth,
            GoldFromCreeps = p.GoldFromCreeps,
            GoldFromNeutrals = p.GoldFromNeutrals,
            GoldFromKills = p.GoldFromKills,
            GoldFromAssists = p.GoldFromAssists,
            GoldFromBuildings = p.GoldFromBuildings,
            StartingGold = p.StartingGold,
            DeathGoldLost = p.DeathGoldLost,
            WardOfSightPlaced = p.WardOfSightPlaced,
            WardOfRevelationPlaced = p.WardOfRevelationPlaced,
            RavenPlaced = p.RavenPlaced,
            Experience = p.Experience,
            Level = p.Level,
            HeroDamage = p.HeroDamage,
            BuildingDamage = p.BuildingDamage,
            Buybacks = p.Buybacks,
            Inventory = new int[]
            {
                p.Inventory48Id,
                p.Inventory49Id,
                p.Inventory50Id,
                p.Inventory51Id,
                p.Inventory52Id,
                p.Inventory53Id,
                p.Inventory63Id,
                p.Inventory64Id,
            }
                .Where(id => id != 0)
                .ToList(),
            RoleIndex = p.RoleIndex,
        };
}
