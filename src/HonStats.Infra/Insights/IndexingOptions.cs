namespace HonStats.Infra.Insights;

public sealed class IndexingOptions
{
    public const string SectionName = "HonStats:Indexing";

    public int ReindexCooldownMinutes { get; set; } = 15;
    public int RecentMatchesLimit { get; set; } = 200;
    public int MatchSummaryConcurrency { get; set; } = 4;
    public int ReindexIntervalHours { get; set; } = 6;
}
