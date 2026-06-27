namespace HonStats.Infra.Insights;

public sealed class TierTimingOptions
{
    public const string SectionName = "HonStats:Insights:TierTiming";

    public int EarlyCutoffMinutes { get; set; } = 6;
    public int LateCutoffMinutes { get; set; } = 20;
}
