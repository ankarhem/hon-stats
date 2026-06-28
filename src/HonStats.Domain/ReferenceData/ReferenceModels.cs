namespace HonStats.Domain.ReferenceData;

public sealed class Hero
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TranslatedName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int PrimaryAttribute { get; set; }
    public string Team { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? RoleDescription { get; set; }
    public int Strength { get; set; }
    public int Agility { get; set; }
    public int Intelligence { get; set; }
    public string? AttackType { get; set; }

    public int AttackDamageMin { get; set; }
    public int AttackDamageMax { get; set; }
    public int AttackRange { get; set; }
    public double AttackSpeed { get; set; }
    public int MoveSpeed { get; set; }

    public double StrengthPerLevel { get; set; }
    public double AgilityPerLevel { get; set; }
    public double IntelligencePerLevel { get; set; }
    public double Armor { get; set; }
    public double MagicArmor { get; set; }
    public double HealthRegen { get; set; }
    public double ManaRegen { get; set; }
    public int SightRangeDay { get; set; }
    public int SightRangeNight { get; set; }

    public int CarryRating { get; set; }
    public int MidRating { get; set; }
    public int HardSupportRating { get; set; }
    public int SoftSupportRating { get; set; }
    public int OffLaneRating { get; set; }
    public int SoloOfflaneRating { get; set; }
    public int JungleRating { get; set; }

    public List<Ability> Abilities { get; set; } = [];

    public string DisplayName => HonText.Strip(TranslatedName);
    public string Slug => HonText.Slugify(TranslatedName);

    public string PrimaryAttributeName =>
        PrimaryAttribute switch
        {
            0 => "Strength",
            1 => "Agility",
            _ => "Intelligence",
        };

    private int PrimaryAttributeValue =>
        PrimaryAttribute switch
        {
            0 => Strength,
            1 => Agility,
            _ => Intelligence,
        };

    public int DisplayDamageMin => AttackDamageMin + PrimaryAttributeValue;
    public int DisplayDamageMax => AttackDamageMax + PrimaryAttributeValue;

    public bool MatchesRole(HeroRole role) =>
        role switch
        {
            HeroRole.Carry => CarryRating > 2,
            HeroRole.Mid => MidRating > 2,
            HeroRole.Offlane => OffLaneRating > 2,
            HeroRole.SoloOfflane => SoloOfflaneRating > 2,
            HeroRole.SoftSupport => SoftSupportRating > 2,
            HeroRole.HardSupport => HardSupportRating > 2,
            HeroRole.Jungle => JungleRating > 2,
            _ => false,
        };
}

public enum HeroRole
{
    Carry,
    Mid,
    Offlane,
    SoloOfflane,
    SoftSupport,
    HardSupport,
    Jungle,
}

public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TranslatedName { get; set; } = string.Empty;
    public int? Cost { get; set; }
    public string IconUrl { get; set; } = string.Empty;
    public List<string> ShopCategories { get; set; } = [];

    public string? Description { get; set; }
    public string? Description2 { get; set; }
    public string? ImpactEffect { get; set; }
    public string? AttackImpactEffect { get; set; }
    public string? TargetType { get; set; }
    public List<Item> Components { get; set; } = [];
    public Dictionary<string, List<double>> Stats { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> ConditionalStats { get; set; } = new();

    public int? ManaCost { get; set; }
    public int? Cooldown { get; set; }
    public int? Range { get; set; }

    public string DisplayName => HonText.NameOnly(TranslatedName);
    public string? Tier => HonText.TierOnly(TranslatedName);
    public string Slug => HonText.Slugify(TranslatedName);
    public bool IsActive => ManaCost is > 0 || Cooldown is > 0 || Range is > 0;

    public int TotalCost => (Cost ?? 0) + Components.Sum(c => c.TotalCost);

    // Percent stats are stored as a fraction (1 = 100%, e.g. castSpeed); flat stats
    // render as-is. An item's own stat fields are already the final aggregate, so
    // bonuses come straight from Stats (no component recursion). Unmapped stats are
    // omitted rather than rendered as misleading raw numbers.
    private static readonly IReadOnlyList<(string Key, string Label, bool Percent)> StatFormats =
    [
        ("strength", "Strength", false),
        ("agility", "Agility", false),
        ("intelligence", "Intelligence", false),
        ("damage", "Damage", false),
        ("armor", "Armor", false),
        ("magicArmor", "Magic Armor", false),
        ("maxHealth", "Max Health", false),
        ("maxMana", "Max Mana", false),
        ("healthRegen", "Health Regen", false),
        ("manaRegen", "Mana Regen", false),
        ("manaRegenMultiplier", "Mana Regeneration", true),
        ("moveSpeed", "Movement Speed", false),
        ("attackRange", "Attack Range", false),
        ("castSpeed", "Cast Speed", true),
        ("attackSpeed", "Attack Speed", true),
        ("evasion", "Evasion", true),
    ];

    public IReadOnlyList<string> PassiveBonuses
    {
        get
        {
            var lines = new List<string>();
            var conditionalKeys = ConditionalStats.Values.SelectMany(d => d.Keys).ToHashSet();

            foreach (var (key, label, percent) in StatFormats)
            {
                if (conditionalKeys.Contains(key))
                {
                    var parts = new List<string>();
                    foreach (var (condition, modStats) in ConditionalStats)
                    {
                        if (!modStats.TryGetValue(key, out var value) || value == 0)
                            continue;
                        var sign = value > 0 ? "+" : "";
                        var rendered = percent ? $"{value * 100:0}%" : $"{value:0.##}";
                        var condLabel = char.ToUpper(condition[0]) + condition[1..];
                        parts.Add($"{sign}{rendered} {label} ({condLabel})");
                    }
                    if (parts.Count > 0)
                        lines.Add(string.Join(" / ", parts));
                    continue;
                }

                if (!Stats.TryGetValue(key, out var values) || values.Count == 0)
                    continue;
                var s = values[0] > 0 ? "+" : "";
                var flat = percent
                    ? string.Join("/", values.Select(v => $"{v * 100:0}")) + "%"
                    : string.Join("/", values.Select(v => $"{v:0.##}"));
                lines.Add($"{s}{flat} {label}");
            }
            return lines;
        }
    }
}

public sealed class Ability
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TranslatedName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<int> ManaCost { get; set; } = [];
    public List<int> Cooldown { get; set; } = [];
    public int Range { get; set; }
    public int TargetRadius { get; set; }
}
