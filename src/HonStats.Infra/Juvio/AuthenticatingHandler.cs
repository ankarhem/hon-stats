using System.Net;
using System.Net.Http.Headers;

namespace HonStats.Infra.Juvio;

// Attaches the service-account bearer token to every outbound juvio request.
// On a 401 it invalidates the cached token and retries once with a fresh one.
public sealed class AuthenticatingHandler : DelegatingHandler
{
    private readonly ITokenProvider tokenProvider;

    public AuthenticatingHandler(ITokenProvider tokenProvider)
    {
        this.tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var response = await this.SendAuthenticatedAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        this.tokenProvider.Invalidate();
        return await this.SendAuthenticatedAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        // A request may be reused for the retry; cloning the headers avoids
        // appending the Authorization header twice.
        var clone = await CloneAsync(request, cancellationToken);
        var token = await this.tokenProvider.GetTokenAsync(cancellationToken);
        clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(clone, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
        };
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
