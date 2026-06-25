namespace HonStats.Domain.ReferenceData;

public sealed class Hero
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TranslatedName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int PrimaryAttribute { get; set; }
    public string Team { get; set; } = string.Empty;
}

public sealed class Item
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TranslatedName { get; set; } = string.Empty;
    public int? Cost { get; set; }
    public string IconUrl { get; set; } = string.Empty;
    public List<string> ShopCategories { get; set; } = [];
}
