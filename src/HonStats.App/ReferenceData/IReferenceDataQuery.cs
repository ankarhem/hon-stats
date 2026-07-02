using HonStats.Domain.ReferenceData;

namespace HonStats.App.ReferenceData;

public interface IReferenceDataQuery
{
    Task<IReadOnlyList<Hero>> GetHeroesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Item>> GetItemsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Ability>> GetAbilitiesAsync(CancellationToken ct = default);
}
