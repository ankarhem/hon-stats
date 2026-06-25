using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HonStats.App.Players;
using HonStats.Infra.Juvio.Adapters;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioPlayerNameResolver(IHttpClientFactory httpClientFactory)
    : IPlayerNameResolver
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;

    public async Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default
    )
    {
        var result = new Dictionary<Guid, ResolvedName>();
        if (accountIds.Count == 0)
            return result;

        var client = httpClientFactory.CreateClient(JuvioHttpClients.Auth);

        foreach (var batch in accountIds.Chunk(BatchSize))
        {
            using var response = await PostWithRetryAsync(client, batch, ct);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var dto = await JsonSerializer.DeserializeAsync<UserInfoCollectionDto>(
                stream,
                JuvioJson.Options,
                ct
            );

            foreach (var user in dto?.UsersValue ?? [])
            {
                var id = Guid.Parse(user.AccountId);
                result[id] = new ResolvedName
                {
                    AccountId = id,
                    DisplayName = user.DisplayName,
                    Username = user.Username,
                    Country = user.Country,
                };
            }
        }

        return result;
    }

    private static async Task<HttpResponseMessage> PostWithRetryAsync(
        HttpClient client,
        Guid[] batch,
        CancellationToken ct
    )
    {
        for (var attempt = 0; ; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/v1/userinfo/getuserinfo",
                new { accountIds = batch },
                cancellationToken: ct
            );
            if (
                response.StatusCode != HttpStatusCode.TooManyRequests
                && (int)response.StatusCode < 500
            )
                return response;

            var status = response.StatusCode;
            response.Dispose();
            if (attempt >= MaxAttempts)
                throw new HttpRequestException(
                    $"getuserinfo failed after {MaxAttempts} retries: {status}"
                );

            await Task.Delay(TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt)), ct);
        }
    }
}
