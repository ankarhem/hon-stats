using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.Juvio;

public interface ITokenPool
{
    Task<string> GetTokenAsync(CancellationToken ct = default);

    void Invalidate(string token);
}

// Round-robin token pool across multiple juvio service accounts. Each account
// has its own cached JWT, semaphore, and installId so re-authenticating one
// account doesn't block the others. Falls back to the single Username/Password
// pair in JuvioOptions when no Accounts are configured.
internal sealed class TokenPool(
    IHttpClientFactory httpClientFactory,
    IOptions<JuvioOptions> options,
    ILogger<TokenPool> logger
) : ITokenPool
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<AccountToken> accounts = BuildAccounts(options.Value);
    private int counter;

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        var index = (uint)Interlocked.Increment(ref this.counter) % (uint)this.accounts.Count;
        var account = this.accounts[(int)index];

        if (account.Token is not null && DateTimeOffset.UtcNow < account.ExpiresAt)
            return account.Token;

        await account.Gate.WaitAsync(ct);
        try
        {
            if (account.Token is not null && DateTimeOffset.UtcNow < account.ExpiresAt)
                return account.Token;

            account.Token = await this.AuthenticateAsync(account, ct);
            account.ExpiresAt = DateTimeOffset.UtcNow.AddHours(1);
            return account.Token;
        }
        finally
        {
            account.Gate.Release();
        }
    }

    public void Invalidate(string token)
    {
        foreach (var account in this.accounts)
        {
            if (account.Token == token)
            {
                account.Token = null;
                account.ExpiresAt = DateTimeOffset.MinValue;
                return;
            }
        }
    }

    private async Task<string> AuthenticateAsync(AccountToken account, CancellationToken ct)
    {
        var juvio = options.Value;
        var client = httpClientFactory.CreateClient(JuvioHttpClients.Auth);

        var query =
            $"?username={Uri.EscapeDataString(account.Username)}"
            + $"&password={Uri.EscapeDataString(account.Password)}"
            + $"&deviceInfo.installId={account.InstallId:D}"
            + $"&clientName={Uri.EscapeDataString(juvio.ClientName)}";

        using var response = await client.PostAsync(
            $"/v1/auth/plainauth{query}",
            content: null,
            ct
        );
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var auth = await JsonSerializer.DeserializeAsync<PlainAuthResponse>(stream, Json, ct);
        if (auth is null || string.IsNullOrEmpty(auth.AuthToken))
            throw new InvalidOperationException("juvio plainauth returned no auth token");

        logger.LogInformation("Authenticated to juvio as {Username}", account.Username);
        return auth.AuthToken;
    }

    private static List<AccountToken> BuildAccounts(JuvioOptions juvio)
    {
        if (juvio.Accounts.Count > 0)
            return juvio.Accounts.Select(a => new AccountToken(a.Username, a.Password)).ToList();

        return [new AccountToken(juvio.Username, juvio.Password)];
    }

    private sealed class PlainAuthResponse
    {
        public string AuthToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class AccountToken(string username, string password)
    {
        public string Username { get; } = username;
        public string Password { get; } = password;
        public Guid InstallId { get; } = Guid.NewGuid();
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public string? Token { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}

public static class JuvioHttpClients
{
    public const string Auth = "juvio-auth";
    public const string GameData = "juvio-gamedata";
    public const string Stats = "juvio-stats";
}
