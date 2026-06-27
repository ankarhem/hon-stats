namespace HonStats.App.Insights;

// Splits a hero's item build into guide-style tiers (Early / Core / Late),
// mirroring the heroesofnewerth.com guide layout. Pure: no I/O, no DI.
//
// Tier rules (decision D1):
//   Early — TotalCost <= EarlyCutoff (cheap items, components, consumables).
//   Core  — TotalCost >  EarlyCutoff AND pick rate in the top half of this hero's
//           >1500 build set (the consistent main build).
//   Late  — TotalCost >  EarlyCutoff AND pick rate in the bottom half
//           (luxury / situational).
//
// The Core/Late split is adaptive per hero: the >1500 items are sorted by pick
// rate descending and halved at the median. The ceiling half lands in Core, so
// odd counts and boundary ties favor Core — a hero's "core" build is the
// generous read (you'd rather over-promote a borderline item than hide it).
public enum GuideTier
{
    Early,
    Core,
    Late,
}

public static class GuideTierClassifier
{
    public const int EarlyCutoff = 1500;

    // Classifies a SINGLE item by cost. Only Early is determinable standalone —
    // Core/Late depend on the rest of the build set, so a >1500 cost throws.
    // Call ClassifySet for the full picture.
    public static GuideTier Classify(int totalCost) =>
        totalCost <= EarlyCutoff
            ? GuideTier.Early
            : throw new InvalidOperationException(
                "Core/Late classification requires the full item set — call ClassifySet."
            );

    // Classifies a SET of items into tiers, returning a map of ItemId -> GuideTier.
    // pickRate = Frequency / Games (computed by the caller; pass 0 when Games is 0).
    public static Dictionary<int, GuideTier> ClassifySet(
        IEnumerable<(int ItemId, int TotalCost, double PickRate)> items
    )
    {
        var list = items.ToList();
        var result = new Dictionary<int, GuideTier>();

        // Early: everything at or below the cost cutoff.
        foreach (var (itemId, totalCost, _) in list.Where(i => i.TotalCost <= EarlyCutoff))
            result[itemId] = GuideTier.Early;

        // Core/Late: split the >1500 items by pick-rate median.
        var above = list.Where(i => i.TotalCost > EarlyCutoff)
            .OrderByDescending(i => i.PickRate)
            .ThenBy(i => i.ItemId)
            .ToList();

        if (above.Count == 0)
            return result;

        // Ceiling half -> Core (odd counts favor Core). Then pull any boundary
        // ties into Core so equal pick rates never straddle the split.
        var split = (above.Count + 1) / 2;
        if (split < above.Count)
        {
            var boundaryRate = above[split - 1].PickRate;
            while (split < above.Count && above[split].PickRate == boundaryRate)
                split++;
        }

        for (var i = 0; i < above.Count; i++)
            result[above[i].ItemId] = i < split ? GuideTier.Core : GuideTier.Late;

        return result;
    }
}
