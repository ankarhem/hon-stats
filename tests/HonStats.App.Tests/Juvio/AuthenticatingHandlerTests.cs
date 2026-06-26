using System.Net;
using AwesomeAssertions;
using HonStats.Infra.Juvio;
using Xunit;

namespace HonStats.App.Tests.Juvio;

public class AuthenticatingHandlerTests
{
    [Fact]
    public async Task AttachesBearerTokenFromPool()
    {
        var pool = new FakeTokenPool("tok-1");
        var inner = new RecordingHandler(_ => Ok());
        using var handler = WithInner(new AuthenticatingHandler(pool), inner);
        using var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://stats.juvio.com/x");
        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.CallCount.Should().Be(1);
        inner.Authorizations.Should().Equal(["Bearer tok-1"]);
    }

    [Fact]
    public async Task RetriesOnceWithFreshToken_OnUnauthorized()
    {
        var pool = new FakeTokenPool("tok-1");
        var inner = new RecordingHandler(call => call == 1 ? Unauthorized() : Ok());
        using var handler = WithInner(new AuthenticatingHandler(pool), inner);
        using var invoker = new HttpMessageInvoker(handler);

        using var request = new HttpRequestMessage(HttpMethod.Get, "https://stats.juvio.com/x");
        using var response = await invoker.SendAsync(request, CancellationToken.None);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        inner.CallCount.Should().Be(2);
        pool.InvalidateCallCount.Should().BeGreaterThanOrEqualTo(1);
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    private static HttpResponseMessage Unauthorized() => new(HttpStatusCode.Unauthorized);

    private static DelegatingHandler WithInner(DelegatingHandler outer, HttpMessageHandler inner)
    {
        outer.InnerHandler = inner;
        return outer;
    }

    private sealed class FakeTokenPool : ITokenPool
    {
        private readonly string token;

        public FakeTokenPool(string token) => this.token = token;

        public int InvalidateCallCount { get; private set; }

        public Task<string> GetTokenAsync(CancellationToken ct = default) =>
            Task.FromResult(this.token);

        public void Invalidate(string token) => this.InvalidateCallCount++;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpResponseMessage> respond;

        public RecordingHandler(Func<int, HttpResponseMessage> respond) => this.respond = respond;

        public int CallCount { get; private set; }

        public List<string?> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            this.CallCount++;
            this.Authorizations.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(this.respond(this.CallCount));
        }
    }
}
