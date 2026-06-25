using System.Net.Http.Json;
using System.Text.Json;
using HonStats.App.Players;
using HonStats.Infra.Juvio.Adapters;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioPlayerNameResolver(IHttpClientFactory httpClientFactory)
    : IPlayerNameResolver
{
    public async Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default
    )
    {
        if (accountIds.Count == 0)
            return new Dictionary<Guid, ResolvedName>();

        var client = httpClientFactory.CreateClient(JuvioHttpClients.Auth);
        using var response = await client.PostAsJsonAsync(
            "/v1/userinfo/getuserinfo",
            new { accountIds = accountIds.ToList() },
            cancellationToken: ct
        );
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<UserInfoCollectionDto>(
            stream,
            JuvioJson.Options,
            ct
        );

        var result = new Dictionary<Guid, ResolvedName>();
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

        return result;
    }
}
