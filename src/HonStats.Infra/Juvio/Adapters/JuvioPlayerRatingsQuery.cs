using System.Text.Json;
using HonStats.App.Players;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioPlayerRatingsQuery(IHttpClientFactory httpClientFactory)
    : IPlayerRatingsQuery
{
    public async Task<PlayerRatings?> GetAsync(Guid accountId, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(
            $"/v1/stats/getprofilestats?userId={accountId}",
            ct
        );
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<ProfileOverviewDto>(
            stream,
            JuvioJson.Options,
            ct
        );
        if (dto is null)
            return null;
        return new PlayerRatings(dto.CurrentMMR, dto.RankedCaldavarRating, dto.RankedMidwarsRating);
    }
}
