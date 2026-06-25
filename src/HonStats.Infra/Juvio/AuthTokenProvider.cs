using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HonStats.Infra.Juvio;

public interface ITokenProvider
{
    Task<string> GetTokenAsync(CancellationToken ct = default);

    void Invalidate();
}

// One server-side service account authenticates against auth.juvio.com and the
// resulting JWT is reused as Bearer for every downstream juvio call. The token
// is cached and re-issued lazily after Invalidate() (e.g. on a downstream 401).
public sealed class AuthTokenProvider : ITokenProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptions<JuvioOptions> options;
    private readonly ILogger<AuthTokenProvider> logger;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Guid installId = Guid.NewGuid();

    private string? cachedToken;
    private DateTimeOffset expiresAt;

    public AuthTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<JuvioOptions> options,
        ILogger<AuthTokenProvider> logger
    )
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options;
        this.logger = logger;
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (this.cachedToken is not null && DateTimeOffset.UtcNow < this.expiresAt)
            return this.cachedToken;

        await this.gate.WaitAsync(ct);
        try
        {
            if (this.cachedToken is not null && DateTimeOffset.UtcNow < this.expiresAt)
                return this.cachedToken;

            this.cachedToken = await this.AuthenticateAsync(ct);
            // Re-authenticate well ahead of any plausible JWT expiry; the exact
            // lifetime is server-controlled and not worth decoding the JWT for.
            this.expiresAt = DateTimeOffset.UtcNow.AddHours(1);
            return this.cachedToken;
        }
        finally
        {
            this.gate.Release();
        }
    }

    public void Invalidate()
    {
        this.cachedToken = null;
        this.expiresAt = DateTimeOffset.MinValue;
    }

    private async Task<string> AuthenticateAsync(CancellationToken ct)
    {
        var juvio = this.options.Value;
        var client = this.httpClientFactory.CreateClient(JuvioHttpClients.Auth);

        // plainauth is POST with credentials in the query string and an empty body.
        var query =
            $"?username={Uri.EscapeDataString(juvio.Username)}"
            + $"&password={Uri.EscapeDataString(juvio.Password)}"
            + $"&deviceInfo.installId={this.installId:D}"
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

        this.logger.LogInformation("Authenticated to juvio as {Username}", juvio.Username);
        return auth.AuthToken;
    }

    private sealed class PlainAuthResponse
    {
        public string AuthToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}

public static class JuvioHttpClients
{
    public const string Auth = "juvio-auth";
    public const string GameData = "juvio-gamedata";
    public const string Stats = "juvio-stats";
}
