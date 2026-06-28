// noqa: SIZE_OK — intentional single gamedata client joining all public endpoints per AGENTS.md.
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
    public int? Strength { get; set; }
    public int? Agility { get; set; }
    public int? Intelligence { get; set; }
    public List<string>? AttackType { get; set; }
    public List<int>? AttackDamageMin { get; set; }
    public List<int>? AttackDamageMax { get; set; }
    public List<int>? AttackRange { get; set; }
    public List<int>? AttackCooldown { get; set; }
    public List<int>? MoveSpeed { get; set; }
    public int CarryRating { get; set; }
    public int MidRating { get; set; }
    public int HardSupportRating { get; set; }
    public int SoftSupportRating { get; set; }
    public int OffLaneRating { get; set; }
    public int JungleRating { get; set; }
    public List<string>? Inventory0 { get; set; }
    public List<string>? Inventory1 { get; set; }
    public List<string>? Inventory2 { get; set; }
    public List<string>? Inventory3 { get; set; }
    public List<string>? Inventory4 { get; set; }
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
    public List<string>? Components { get; set; }
    public List<int>? ManaCost { get; set; }
    public List<int>? CooldownTime { get; set; }
    public List<int>? Range { get; set; }
    public List<double>? Strength { get; set; }
    public List<double>? Agility { get; set; }
    public List<double>? Intelligence { get; set; }
    public List<double>? Damage { get; set; }
    public List<double>? Armor { get; set; }
    public List<double>? MagicArmor { get; set; }
    public List<double>? MaxHealth { get; set; }
    public List<double>? MaxMana { get; set; }
    public List<double>? HealthRegen { get; set; }
    public List<double>? ManaRegen { get; set; }
    public List<double>? ManaRegenMultiplier { get; set; }
    public List<double>? MoveSpeed { get; set; }
    public List<double>? AttackRange { get; set; }
    public List<double>? CastSpeed { get; set; }
    public List<string>? TargetScheme { get; set; }
}

internal sealed class GetAllAbilitiesDto
{
    public List<AbilityDto>? Abilities { get; set; }
    public List<AbilityDto> AbilitiesValue => this.Abilities ?? [];
}

internal sealed class AbilityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TranslatedName { get; set; }
    public List<string>? Icon { get; set; }
    public List<int>? ManaCost { get; set; }
    public List<int>? CooldownTime { get; set; }
    public List<int>? Range { get; set; }
    public List<int>? TargetRadius { get; set; }
}

internal sealed class StringsDto
{
    public string? Language { get; set; }
    public Dictionary<string, string>? Strings { get; set; }
}

internal sealed class EntityOverridesDto
{
    public Dictionary<string, Dictionary<string, double>> Heroes { get; set; } = new();
    public Dictionary<string, ItemOverrideDto> Items { get; set; } = new();
    public List<string> PhoenixRewards { get; set; } = [];
}

internal sealed class ItemOverrideDto
{
    public Dictionary<string, double> Stats { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> Modifiers { get; set; } = new();
}

// Fetches heroes, items, abilities + strings from the public gamedata service.
// Heroes are enriched with their abilities (resolved via inventory0-4 name refs
// joined to /entities/abilities) and descriptions (from /strings). Stateless,
// safe as a singleton.
internal sealed class GamedataClient(IHttpClientFactory httpClientFactory)
{
    private static readonly EntityOverridesDto EntityOverrides = LoadEntityOverrides();

    private static EntityOverridesDto LoadEntityOverrides()
    {
        var asm = typeof(GamedataClient).Assembly;
        var name = $"{asm.GetName().Name}.ReferenceData.entity-overrides.json";
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null)
            return new EntityOverridesDto();
        return JsonSerializer.Deserialize<EntityOverridesDto>(stream, GamedataJson.Options)
            ?? new EntityOverridesDto();
    }

    public async Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken ct)
    {
        var heroesTask = Get<GetAllHeroesDto>("/entities/heroes", ct);
        var abilitiesTask = Get<GetAllAbilitiesDto>("/entities/abilities", ct);
        var stringsTask = GetStrings(ct);
        await Task.WhenAll(heroesTask, abilitiesTask, stringsTask);

        var abilitiesByName = (abilitiesTask.Result?.AbilitiesValue ?? [])
            .Where(a => !string.IsNullOrEmpty(a.Name))
            .DistinctBy(a => a.Name)
            .ToDictionary(a => a.Name, a => a);
        var strings = stringsTask.Result;

        return (heroesTask.Result?.HeroesValue ?? [])
            .Select(h => MapHero(h, abilitiesByName, strings))
            .ToList();
    }

    public async Task<IReadOnlyList<Item>> GetItemsAsync(CancellationToken ct)
    {
        var itemsTask = Get<GetAllItemsDto>("/entities/items", ct);
        var stringsTask = GetStrings(ct);
        await Task.WhenAll(itemsTask, stringsTask);

        var strings = stringsTask.Result;
        var dtos = itemsTask.Result?.ItemsValue ?? [];

        var items = dtos.Select(d => MapItem(d, strings)).ToList();
        var byName = items
            .Where(i => !string.IsNullOrEmpty(i.Name))
            .DistinctBy(i => i.Name)
            .ToDictionary(i => i.Name, i => i);

        foreach (var (dto, item) in dtos.Zip(items))
        {
            item.Components = ParseComponentNames(dto.Components)
                .Where(byName.ContainsKey)
                .Select(n => byName[n])
                .ToList();
        }

        return items;
    }

    private static IEnumerable<string> ParseComponentNames(List<string>? components) =>
        (components ?? []).SelectMany(s =>
            s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        );

    private async Task<T?> Get<T>(string relativeUri, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(JuvioHttpClients.GameData);
        using var response = await client.GetAsync(relativeUri, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, GamedataJson.Options, ct);
    }

    private async Task<Dictionary<string, string>> GetStrings(CancellationToken ct)
    {
        var dto = await Get<StringsDto>("/strings", ct);
        return dto?.Strings ?? new Dictionary<string, string>();
    }

    private static Hero MapHero(
        HeroDto h,
        Dictionary<string, AbilityDto> abilitiesByName,
        Dictionary<string, string> strings
    )
    {
        var abilityNames = new[]
        {
            h.Inventory0,
            h.Inventory1,
            h.Inventory2,
            h.Inventory3,
            h.Inventory4,
        }
            .Where(list => list is { Count: > 0 })
            .Select(list => list![0])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !name.Contains("AttributeBoost", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var heroAbilities = abilityNames
            .Where(name => abilitiesByName.ContainsKey(name))
            .Select(name => MapAbility(abilitiesByName[name], strings))
            .ToList();

        var attackCooldown = FirstInt(h.AttackCooldown);
        EntityOverrides.Heroes.TryGetValue(h.Name, out var heroOverride);

        return new Hero
        {
            Id = h.Id,
            Name = h.Name,
            TranslatedName = string.IsNullOrWhiteSpace(h.TranslatedName)
                ? h.Name
                : h.TranslatedName,
            IconUrl = FirstIcon(h.Icon),
            PrimaryAttribute = h.PrimaryAttribute,
            Team = h.Team ?? string.Empty,
            Description = strings.GetValueOrDefault($"{h.Name}_description"),
            RoleDescription = strings.GetValueOrDefault($"{h.Name}_role"),
            Strength = h.Strength ?? 0,
            Agility = h.Agility ?? 0,
            Intelligence = h.Intelligence ?? 0,
            AttackType = h.AttackType is { Count: > 0 } ? h.AttackType[0] : null,
            AttackDamageMin = FirstInt(h.AttackDamageMin),
            AttackDamageMax = FirstInt(h.AttackDamageMax),
            AttackRange = FirstInt(h.AttackRange),
            AttackSpeed = attackCooldown > 0 ? Math.Round(1000.0 / attackCooldown, 2) : 0,
            MoveSpeed = FirstInt(h.MoveSpeed),
            StrengthPerLevel = GetOverride(heroOverride, "strengthPerLevel"),
            AgilityPerLevel = GetOverride(heroOverride, "agilityPerLevel"),
            IntelligencePerLevel = GetOverride(heroOverride, "intelligencePerLevel"),
            Armor = GetOverride(heroOverride, "armor"),
            MagicArmor = GetOverride(heroOverride, "magicArmor"),
            HealthRegen = GetOverride(heroOverride, "healthRegen"),
            ManaRegen = GetOverride(heroOverride, "manaRegen"),
            SightRangeDay = (int)GetOverride(heroOverride, "sightRangeDay"),
            SightRangeNight = (int)GetOverride(heroOverride, "sightRangeNight"),
            CarryRating = h.CarryRating,
            MidRating = h.MidRating,
            HardSupportRating = h.HardSupportRating,
            SoftSupportRating = h.SoftSupportRating,
            OffLaneRating = h.OffLaneRating,
            JungleRating = h.JungleRating,
            Abilities = heroAbilities,
        };
    }

    private static double GetOverride(Dictionary<string, double>? overrides, string key) =>
        overrides is not null && overrides.TryGetValue(key, out var value) ? value : 0;

    private static Item MapItem(ItemDto i, Dictionary<string, string> strings)
    {
        var stats = new Dictionary<string, List<double>>();
        AddStat(stats, "strength", i.Strength);
        AddStat(stats, "agility", i.Agility);
        AddStat(stats, "intelligence", i.Intelligence);
        AddStat(stats, "damage", i.Damage);
        AddStat(stats, "armor", i.Armor);
        AddStat(stats, "magicArmor", i.MagicArmor);
        AddStat(stats, "maxHealth", i.MaxHealth);
        AddStat(stats, "maxMana", i.MaxMana);
        AddStat(stats, "healthRegen", i.HealthRegen);
        AddStat(stats, "manaRegen", i.ManaRegen);
        AddStat(stats, "manaRegenMultiplier", i.ManaRegenMultiplier);
        AddStat(stats, "moveSpeed", i.MoveSpeed);
        AddStat(stats, "attackRange", i.AttackRange);
        AddStat(stats, "castSpeed", i.CastSpeed);

        EntityOverrides.Items.TryGetValue(i.Name, out var itemOverride);

        if (itemOverride is not null)
        {
            foreach (var (key, value) in itemOverride.Stats)
                stats[key] = [value];
        }

        var conditionalStats = new Dictionary<string, Dictionary<string, double>>();
        if (itemOverride?.Modifiers is { Count: > 0 })
        {
            foreach (var (condition, modStats) in itemOverride.Modifiers)
                conditionalStats[condition] = new Dictionary<string, double>(modStats);
        }

        var shopCategories = (i.ShopCategories ?? [])
            .SelectMany(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .ToList();
        if (EntityOverrides.PhoenixRewards.Contains(i.Name))
            shopCategories.Add("phoenix_rewards");

        var cooldown = FirstInt(i.CooldownTime);
        return new Item
        {
            Id = i.Id,
            Name = i.Name,
            TranslatedName = string.IsNullOrWhiteSpace(i.TranslatedName)
                ? i.Name
                : i.TranslatedName,
            Cost = i.Cost,
            IconUrl = ResolveIcon(i.Icon, i.Id),
            ShopCategories = shopCategories,
            Description = strings.GetValueOrDefault($"{i.Name}_description"),
            Description2 = strings.GetValueOrDefault($"{i.Name}_description2"),
            ImpactEffect = ResolveImpactEffect(i.Name, strings),
            AttackImpactEffect = ResolveAttackImpactEffect(i.Name, strings),
            TargetType = MapTargetScheme(i.TargetScheme),
            Stats = stats,
            ConditionalStats = conditionalStats,
            ManaCost = FirstInt(i.ManaCost) is var m && m > 0 ? m : null,
            Cooldown = cooldown > 0 ? cooldown / 1000 : null,
            Range = FirstInt(i.Range) is var r && r > 0 ? r : null,
        };
    }

    private static void AddStat(
        Dictionary<string, List<double>> stats,
        string key,
        List<double>? values
    )
    {
        if (values is { Count: > 0 } && values[0] != 0)
            stats[key] = values;
    }

    // IMPACT_effect = active cast effect text; ATTACK_IMPACT_effect = on-hit combat text.
    // Prefer the exact key (no colon suffix); fall back to any colon variant that
    // isn't a _shopdescription.
    private static string? ResolveImpactEffect(
        string itemName,
        Dictionary<string, string> strings
    ) => ResolveEffect(itemName, "IMPACT_effect", strings);

    private static string? ResolveAttackImpactEffect(
        string itemName,
        Dictionary<string, string> strings
    ) => ResolveEffect(itemName, "ATTACK_IMPACT_effect", strings);

    private static string? ResolveEffect(
        string itemName,
        string suffix,
        Dictionary<string, string> strings
    )
    {
        var baseKey = $"{itemName}_{suffix}";
        if (strings.TryGetValue(baseKey, out var exact))
            return exact;
        return strings
            .Keys.Where(k => k.StartsWith($"{baseKey}:", StringComparison.Ordinal))
            .Where(k => !k.EndsWith("_shopdescription", StringComparison.OrdinalIgnoreCase))
            .Select(k => strings[k])
            .FirstOrDefault();
    }

    // targetScheme is per-level (array); take the most permissive (last/highest level).
    private static string? MapTargetScheme(List<string>? scheme)
    {
        if (scheme is null || scheme.Count == 0)
            return null;
        var value = scheme[^1];
        return value switch
        {
            "self" => "Self",
            "enemy_units" => "Enemy Units",
            "enemy_units_and_self" => "Self / Enemy Units",
            "enemy_and_willing_heroes" => "Enemy / Willing Heroes",
            "enemy_nonhero_nonboss_nonboss_units_and_trees_and_gadgets" =>
                "Enemy Non-Boss Units, Trees & Gadgets",
            "enemy_nonboss_npc_units" => "Enemy Non-Boss Units",
            "ally_units" => "Ally Units",
            "ally_heroes" => "Ally Heroes",
            "ally_mana_units" => "Ally Mana Units",
            "other_ally_units" => "Other Ally Units",
            "other_ally_heroes" => "Other Ally Heroes",
            "other_heroes" => "Other Heroes",
            "all_units" => "All Units",
            "all_heroes" => "All Heroes",
            "trees" => "Trees",
            _ => null,
        };
    }

    private static Ability MapAbility(AbilityDto a, Dictionary<string, string> strings) =>
        new()
        {
            Id = a.Id,
            Name = a.Name,
            TranslatedName = string.IsNullOrWhiteSpace(a.TranslatedName)
                ? a.Name
                : a.TranslatedName,
            IconUrl = FirstIcon(a.Icon),
            Description =
                strings.GetValueOrDefault($"{a.Name}_description_simple")
                ?? strings.GetValueOrDefault($"{a.Name}_description2")
                ?? strings.GetValueOrDefault($"{a.Name}_description"),
            ManaCost = a.ManaCost ?? [],
            Cooldown = (a.CooldownTime ?? []).Select(ms => ms / 1000).ToList(),
            Range = FirstInt(a.Range),
            TargetRadius = FirstInt(a.TargetRadius),
        };

    private static string FirstIcon(List<string>? icons) =>
        icons is { Count: > 0 } ? icons[0] : string.Empty;

    private static string ResolveIcon(List<string>? icons, int id)
    {
        var icon = FirstIcon(icons);
        return !string.IsNullOrEmpty(icon)
            ? icon
            : $"https://gamestorage.juvio.com/items/{id}/icon.webp";
    }

    private static int FirstInt(List<int>? values) => values is { Count: > 0 } ? values[0] : 0;
}
