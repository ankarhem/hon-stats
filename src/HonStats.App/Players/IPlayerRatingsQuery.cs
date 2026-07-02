namespace HonStats.App.Players;

public interface IPlayerRatingsQuery
{
    Task<PlayerRatings?> GetAsync(Guid accountId, CancellationToken ct = default);
}

public sealed record PlayerRatings(
    double CurrentMmr,
    double RankedCaldavarRating,
    double RankedMidwarsRating
);
