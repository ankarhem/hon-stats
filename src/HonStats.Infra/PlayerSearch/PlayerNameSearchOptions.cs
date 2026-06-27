namespace HonStats.Infra.PlayerSearch;

public sealed class PlayerNameSearchOptions
{
    public const string SectionName = "HonStats:PlayerNameSearch";

    // Cadence at which the seed service checks staleness / rebuilds the in-memory
    // matcher. Bounds rebuild rate to one per interval during indexing bursts while
    // making the index fresh within ~PollInterval of a PlayerMatchesIndexed event.
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
}
