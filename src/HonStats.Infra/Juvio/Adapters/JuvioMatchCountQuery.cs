using System.Text.Json;
using HonStats.App.Leaderboard;
using Microsoft.Extensions.Caching.Memory;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioMatchCountQuery(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    : IMatchCountQuery
{
    public async Task<MatchCounts?> GetAsync(CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            "juvio:matchcount",
            async _ =>
            {
                var dto = await GetAsync<NumberOfMatchesDto>("/v1/stats/getnumberofmatches", ct);
                return dto is null
                    ? null
                    : new MatchCounts(dto.TotalMatches, dto.FocMatches, dto.MidwarsMatches);
            },
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            }
        );
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Stats);
        using var response = await client.GetAsync(path, ct);
        if (!response.IsSuccessStatusCode)
            return null;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, JuvioJson.Options, ct);
    }
}
