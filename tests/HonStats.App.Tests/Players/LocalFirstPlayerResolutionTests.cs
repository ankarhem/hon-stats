using AwesomeAssertions;
using HonStats.App.Players;
using HonStats.Domain.Players;
using HonStats.Infra.Players;
using Xunit;

namespace HonStats.App.Tests.Players;

public class LocalFirstPlayerResolutionTests
{
    [Fact]
    public async Task Search_WithKnownUsername_ReturnsFromStoreWithoutCallingJuvio()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var store = new FakePlayerNameStore();
        await store.UpsertAsync(id, "Chad", "Chadster", "US", CancellationToken.None);
        store.Upserts.Clear(); // discard the setup upsert; we only care about SUT-driven writes

        var juvio = new FakeJuvioSearch([new() { AccountId = id, Username = "Chad" }]);
        var search = new LocalFirstPlayerSearch(juvio, store);

        var results = await search.SearchAsync("chad", CancellationToken.None);

        results
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(new { AccountId = id, Username = "chad" });
        juvio.Calls.Should().Be(0);
        store.Upserts.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_WithUnknownUsername_FallsBackToJuvioAndWriteThroughsToStore()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var store = new FakePlayerNameStore();
        var juvio = new FakeJuvioSearch([new() { AccountId = id, Username = "Chad" }]);
        var search = new LocalFirstPlayerSearch(juvio, store);

        var results = await search.SearchAsync("chad", CancellationToken.None);

        results.Should().ContainSingle();
        juvio.Calls.Should().Be(1);
        store
            .Upserts.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    AccountId = id,
                    Username = "Chad",
                    DisplayName = (string?)null,
                    Country = (string?)null,
                }
            );
    }

    [Fact]
    public async Task Search_WithNoJuvioMatch_DoesNotWriteThrough()
    {
        var store = new FakePlayerNameStore();
        var juvio = new FakeJuvioSearch([]);
        var search = new LocalFirstPlayerSearch(juvio, store);

        var results = await search.SearchAsync("nobody", CancellationToken.None);

        results.Should().BeEmpty();
        juvio.Calls.Should().Be(1);
        store.Upserts.Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_WhenAllIdsKnownLocally_DoesNotCallJuvio()
    {
        var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var store = new FakePlayerNameStore();
        await store.UpsertAsync(a, "alpha", "Alpha One", null, CancellationToken.None);
        await store.UpsertAsync(b, "bravo", null, "NO", CancellationToken.None);

        var juvio = new FakeJuvioResolver(new Dictionary<Guid, ResolvedName>());
        var resolver = new LocalFirstPlayerNameResolver(juvio, store);

        var result = await resolver.ResolveAsync(new[] { a, b }, CancellationToken.None);

        result.Should().HaveCount(2).And.ContainKeys(a, b);
        result[a].DisplayName.Should().Be("Alpha One");
        result[b].Country.Should().Be("NO");
        juvio.Calls.Should().BeEmpty();
        store.Upserts.Should().HaveCount(2); // only the two setup upserts
    }

    [Fact]
    public async Task Resolve_WhenSomeIdsUnknown_CallsJuvioOnlyForMissesAndWriteThroughsThem()
    {
        var a = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var b = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var c = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var store = new FakePlayerNameStore();
        await store.UpsertAsync(a, "alpha", null, null, CancellationToken.None);

        var juvio = new FakeJuvioResolver(
            new Dictionary<Guid, ResolvedName>
            {
                [b] = new()
                {
                    AccountId = b,
                    Username = "bravo",
                    Country = "NO",
                },
                [c] = new()
                {
                    AccountId = c,
                    Username = "charlie",
                    Country = "SE",
                },
            }
        );
        var resolver = new LocalFirstPlayerNameResolver(juvio, store);

        var result = await resolver.ResolveAsync(new[] { a, b, c }, CancellationToken.None);

        result.Should().HaveCount(3);
        result[a].Username.Should().Be("alpha");
        result[b].Username.Should().Be("bravo");
        result[b].Country.Should().Be("NO");
        result[c].Username.Should().Be("charlie");

        juvio.Calls.Should().ContainSingle().Which.Should().BeEquivalentTo(new[] { b, c });
        store
            .Upserts.Should()
            .Contain(u => u.AccountId == b && u.Username == "bravo" && u.Country == "NO");
        store
            .Upserts.Should()
            .Contain(u => u.AccountId == c && u.Username == "charlie" && u.Country == "SE");
    }

    private sealed record UpsertCall(
        Guid AccountId,
        string? Username,
        string? DisplayName,
        string? Country
    );

    private sealed class FakePlayerNameStore : IPlayerNameStore
    {
        private readonly Dictionary<Guid, ResolvedName> _rows = new();

        public List<UpsertCall> Upserts { get; } = new();

        public Task<Guid?> GetAccountIdByUsernameAsync(
            string username,
            CancellationToken ct = default
        )
        {
            var needle = username.ToLowerInvariant();
            var hit = _rows.Values.FirstOrDefault(r => r.Username?.ToLowerInvariant() == needle);
            return Task.FromResult(hit is null ? null : (Guid?)hit.AccountId);
        }

        public Task<IReadOnlyDictionary<Guid, ResolvedName>> GetByAccountIdsAsync(
            IReadOnlyList<Guid> accountIds,
            CancellationToken ct = default
        )
        {
            var result = accountIds
                .Where(_rows.ContainsKey)
                .ToDictionary(id => id, id => _rows[id]);
            IReadOnlyDictionary<Guid, ResolvedName> typed = result;
            return Task.FromResult(typed);
        }

        public Task UpsertAsync(
            Guid accountId,
            string? username,
            string? displayName,
            string? country,
            CancellationToken ct = default
        )
        {
            Upserts.Add(new UpsertCall(accountId, username, displayName, country));
            var existing = _rows.GetValueOrDefault(accountId);
            _rows[accountId] = new ResolvedName
            {
                AccountId = accountId,
                Username = username ?? existing?.Username,
                DisplayName = displayName ?? existing?.DisplayName,
                Country = country ?? existing?.Country,
            };
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJuvioSearch(IReadOnlyList<PlayerSearchResult> results) : IPlayerSearch
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<PlayerSearchResult>> SearchAsync(
            string username,
            CancellationToken ct = default
        )
        {
            Calls++;
            return Task.FromResult(results);
        }
    }

    private sealed class FakeJuvioResolver(IReadOnlyDictionary<Guid, ResolvedName> names)
        : IPlayerNameResolver
    {
        public List<IReadOnlyCollection<Guid>> Calls { get; } = new();

        public Task<IReadOnlyDictionary<Guid, ResolvedName>> ResolveAsync(
            IReadOnlyCollection<Guid> accountIds,
            CancellationToken ct = default
        )
        {
            Calls.Add(accountIds.ToList());
            var result = accountIds
                .Where(names.ContainsKey)
                .ToDictionary(id => id, id => names[id]);
            IReadOnlyDictionary<Guid, ResolvedName> typed = result;
            return Task.FromResult(typed);
        }
    }
}
