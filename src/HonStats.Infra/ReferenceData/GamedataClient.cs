using System.Text.Json;
using HonStats.Domain.ReferenceData;
using HonStats.Infra.Juvio;

namespace HonStats.Infra.ReferenceData;

internal static class GamedataJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

internal sealed class GetAllHeroesDto
{
    public List<HeroDto>? Heroes { get; set; }
    public List<HeroDto> HeroesValue => this.Heroes ?? [];
}

internal sealed class HeroDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TranslatedName { get; set; }
    public int PrimaryAttribute { get; set; }
    public string? Team { get; set; }
    public List<string>? Icon { get; set; }
}

internal sealed class GetAllItemsDto
{
    public List<ItemDto>? Items { get; set; }
    public List<ItemDto> ItemsValue => this.Items ?? [];
}

internal sealed class ItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TranslatedName { get; set; }
    public int? Cost { get; set; }
    public List<string>? Icon { get; set; }
    public List<string>? ShopCategories { get; set; }
}

// Fetches heroes + items from the public gamedata service. Stateless, safe as a singleton.
internal sealed class GamedataClient(IHttpClientFactory httpClientFactory)
{
    public async Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken ct)
    {
        var dto = await this.Get<GetAllHeroesDto>("/entities/heroes", ct);
        return (dto?.HeroesValue ?? []).Select(MapHero).ToList();
    }

    public async Task<IReadOnlyList<Item>> GetItemsAsync(CancellationToken ct)
    {
        var dto = await this.Get<GetAllItemsDto>("/entities/items", ct);
        return (dto?.ItemsValue ?? []).Select(MapItem).ToList();
    }

    private async Task<T?> Get<T>(string relativeUri, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.GameData);
        using var response = await client.GetAsync(relativeUri, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, GamedataJson.Options, ct);
    }

    private static Hero MapHero(HeroDto h) =>
        new()
        {
            Id = h.Id,
            Name = h.Name,
            TranslatedName = string.IsNullOrWhiteSpace(h.TranslatedName)
                ? h.Name
                : h.TranslatedName,
            IconUrl = FirstIcon(h.Icon),
            PrimaryAttribute = h.PrimaryAttribute,
            Team = h.Team ?? string.Empty,
        };

    private static Item MapItem(ItemDto i) =>
        new()
        {
            Id = i.Id,
            Name = i.Name,
            TranslatedName = string.IsNullOrWhiteSpace(i.TranslatedName)
                ? i.Name
                : i.TranslatedName,
            Cost = i.Cost,
            IconUrl = FirstIcon(i.Icon),
            ShopCategories = i.ShopCategories ?? [],
        };

    private static string FirstIcon(List<string>? icons) =>
        icons is { Count: > 0 } ? icons[0] : string.Empty;
}
