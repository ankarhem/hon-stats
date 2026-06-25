using HonStats.App.ReferenceData;
using HonStats.Domain.ReferenceData;

namespace HonStats.Infra.ReferenceData;

// Singleton in-memory cache for heroes + items. Lazy-loads on first read and is
// periodically refreshed by ReferenceDataRefreshService. Registered as IReferenceDataQuery.
internal sealed class CachedReferenceData(GamedataClient gamedata)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyList<Hero> heroes = [];
    private IReadOnlyList<Item> items = [];
    private bool loaded;

    public async Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken ct)
    {
        await this.EnsureLoadedAsync(ct);
        return this.heroes;
    }

    public async Task<IReadOnlyList<Item>> GetItemsAsync(CancellationToken ct)
    {
        await this.EnsureLoadedAsync(ct);
        return this.items;
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        await this.gate.WaitAsync(ct);
        try
        {
            var heroesTask = gamedata.GetHeroesAsync(ct);
            var itemsTask = gamedata.GetItemsAsync(ct);
            await Task.WhenAll(heroesTask, itemsTask);
            this.heroes = heroesTask.Result;
            this.items = itemsTask.Result;
            this.loaded = true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (this.loaded)
            return;
        await this.RefreshAsync(ct);
    }
}

// IReferenceDataQuery adapter over the singleton cache.
internal sealed class CachedReferenceDataQuery(CachedReferenceData cache) : IReferenceDataQuery
{
    public Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken ct = default) =>
        cache.GetHeroesAsync(ct);

    public Task<IReadOnlyList<Item>> GetItemsAsync(CancellationToken ct = default) =>
        cache.GetItemsAsync(ct);
}
