using System.Net.Http.Json;
using System.Text.Json;
using HonStats.App.Players;
using HonStats.Domain.Players;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioPlayerProfileQuery(
    IHttpClientFactory httpClientFactory,
    IPlayerNameResolver nameResolver
) : IPlayerProfileQuery
{
    public async Task<PlayerProfile?> GetAsync(Guid accountId, CancellationToken ct = default)
    {
        var identity = await this.GetIdentityAsync(accountId, ct);
        if (identity is null)
            return null;

        var overviewTask = this.GetOverviewAsync(accountId, ct);
        var summaryTask = this.GetSummaryAsync(accountId, ct);
        var rankTask = this.GetRankAsync(accountId, ct);
        await Task.WhenAll(overviewTask, summaryTask, rankTask);

        return new PlayerProfile
        {
            Identity = identity,
            Overview = MapOverview(overviewTask.Result),
            Summary = MapSummary(summaryTask.Result),
            Rank = rankTask.Result is null ? null : MapRank(rankTask.Result),
        };
    }

    private async Task<PlayerIdentity?> GetIdentityAsync(Guid accountId, CancellationToken ct)
    {
        var resolved = await nameResolver.ResolveAsync([accountId], ct);
        if (!resolved.TryGetValue(accountId, out var name))
            return null;

        return new PlayerIdentity
        {
            AccountId = accountId,
            DisplayName = name.DisplayName ?? name.Username ?? accountId.ToString(),
            Username = name.Username ?? string.Empty,
            Country = name.Country,
            CreatedAt = name.CreatedAt,
        };
    }

    private async Task<ProfileOverviewDto?> GetOverviewAsync(Guid accountId, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(
            $"/v1/stats/getprofilestats?userId={accountId}",
            ct
        );
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<ProfileOverviewDto>(
            stream,
            JuvioJson.Options,
            ct
        );
    }

    private async Task<ProfileSummaryDto?> GetSummaryAsync(Guid accountId, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(
            $"/v1/stats/getplayersummary?accountId={accountId}",
            ct
        );
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<ProfileSummaryDto>(
            stream,
            JuvioJson.Options,
            ct
        );
    }

    private async Task<PlayerRankDto?> GetRankAsync(Guid accountId, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(
            $"/v1/stats/getplayerrank?accountId={accountId}",
            ct
        );
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<PlayerRankDto>(stream, JuvioJson.Options, ct);
    }

    private static ProfileOverview MapOverview(ProfileOverviewDto? dto)
    {
        if (dto is null)
            return new ProfileOverview();

        return new ProfileOverview
        {
            AverageKDA = dto.AverageKDA,
            AverageDPM = dto.AverageDPM,
            AverageGPM = dto.AverageGPM,
            AverageXPM = dto.AverageXPM,
            CurrentMMR = dto.CurrentMMR,
            PublicSkillRating = dto.PublicSkillRating,
            RankedCaldavarRating = dto.RankedCaldavarRating,
            RankedMidwarsRating = dto.RankedMidwarsRating,
            MatchesPlayed = dto.MatchesPlayed,
            WinRate = dto.WinRate,
            LastPlayed = dto.LastPlayed,
            AllTimeRecord = new StatsRecord
            {
                Mvps = dto.AllTimeRecord.Mvps,
                Annihilations = dto.AllTimeRecord.Annihilations,
                Smackdowns = dto.AllTimeRecord.Smackdowns,
                Killstreak = dto.AllTimeRecord.Killstreak,
                WardsPlaced = dto.AllTimeRecord.WardsPlaced,
                WardsDestroyed = dto.AllTimeRecord.WardsDestroyed,
                GoldEarned = dto.AllTimeRecord.GoldEarned,
                GamesPlayed = dto.AllTimeRecord.GamesPlayed,
                GamesWon = dto.AllTimeRecord.GamesWon,
            },
            TopRoles = dto
                .TopRolesValue.Select(r => new RoleStat
                {
                    Role = r.Role,
                    WinRate = r.WinRate,
                    Kills = r.Kills,
                    Deaths = r.Deaths,
                    Assists = r.Assists,
                    GamesPlayed = r.GamesPlayed,
                })
                .ToList(),
            TopHeroes = dto
                .TopHeroesValue.Select(h => new HeroStat
                {
                    HeroName = h.HeroName,
                    HeroImageUrl = h.HeroImageUrl,
                    HeroImageTgaUrl = h.HeroImageTgaUrl,
                    WinRate = h.WinRate,
                    Kills = h.Kills,
                    Deaths = h.Deaths,
                    Assists = h.Assists,
                })
                .ToList(),
            NewPlayer = dto.NewPlayer,
        };
    }

    private static ProfileSummary MapSummary(ProfileSummaryDto? dto)
    {
        if (dto is null)
            return new ProfileSummary();

        return new ProfileSummary
        {
            TotalKills = dto.TotalKills,
            TotalDeaths = dto.TotalDeaths,
            TotalAssists = dto.TotalAssists,
            TotalCreepKills = dto.TotalCreepKills,
            TotalNeutralKills = dto.TotalNeutralKills,
            TotalBuybacks = dto.TotalBuybacks,
            TotalGames = dto.TotalGames,
            TotalHellbourne = dto.TotalHellbourne,
            TotalLegion = dto.TotalLegion,
            TotalWins = dto.TotalWins,
            TotalLoss = dto.TotalLoss,
            WinRate = dto.WinRate,
            KillDeathRatio = dto.KillDeathRatio,
            KillDeathAssistRatio = dto.KillDeathAssistRatio,
            AssistDeathRatio = dto.AssistDeathRatio,
            SkillRating = dto.SkillRating,
            RankedCaldavarRating = dto.RankedCaldavarRating,
            RankedMidwarsRating = dto.RankedMidwarsRating,
        };
    }

    private static PlayerRank MapRank(PlayerRankDto dto) =>
        new()
        {
            CurrentMmr = dto.CurrentMmr,
            RankName = dto.RankName,
            StarLevel = dto.StarLevel,
            Icon = dto.Icon,
            Color = dto.Color,
            MinMmr = dto.MinMmr,
            MaxMmr = dto.MaxMmr,
            IsTopRank = dto.IsTopRank,
            LastUpdated = dto.LastUpdated,
        };
}
