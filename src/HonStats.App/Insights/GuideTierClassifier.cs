namespace HonStats.App.Insights;

// Splits a hero's item build into guide-style tiers (Early / Core / Late)
// by mean first-buy time. Pure: no I/O, no DI.
public enum GuideTier
{
    Early,
    Core,
    Late,
}

public static class GuideTierClassifier
{
    public static Dictionary<int, GuideTier> ClassifyByTime(
        IEnumerable<(int ItemId, double AvgSeconds)> items,
        int earlyCutoffMinutes,
        int lateCutoffMinutes
    )
    {
        var earlySec = earlyCutoffMinutes * 60;
        var lateSec = lateCutoffMinutes * 60;

        var result = new Dictionary<int, GuideTier>();
        foreach (var (itemId, avgSeconds) in items)
        {
            result[itemId] =
                avgSeconds <= 0 || avgSeconds < earlySec ? GuideTier.Early
                : avgSeconds < lateSec ? GuideTier.Core
                : GuideTier.Late;
        }

        return result;
    }
}
