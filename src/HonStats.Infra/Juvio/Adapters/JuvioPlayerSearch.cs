using System.Text.Json;
using HonStats.App.Players;
using HonStats.Domain.Players;

namespace HonStats.Infra.Juvio.Adapters;

internal sealed class JuvioPlayerSearch(IHttpClientFactory httpClientFactory) : IPlayerSearch
{
    public async Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(
        string username,
        CancellationToken ct = default
    )
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Auth);
        var encoded = Uri.EscapeDataString(username.Trim());
        using var response = await client.GetAsync(
            $"/v1/userinfo/getidbyusername?Usernames={encoded}",
            ct
        );
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var dto = await JsonSerializer.DeserializeAsync<AccountsCollectionDto>(
            stream,
            JuvioJson.Options,
            ct
        );
        return (dto?.AccountsValue ?? []).Select(Map).ToList();
    }

    private static PlayerSearchResult Map(AccountListInfoDto account) =>
        new() { AccountId = Guid.Parse(account.AccountId), Username = account.Username };
}
