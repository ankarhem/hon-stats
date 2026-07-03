using HonStats.Domain.ReferenceData;

namespace HonStats.Web;

public sealed record ItemTooltipView(Item Item, string IconUrl, BuildStatsView? Stats = null);

public sealed record AbilityTooltipView(Ability Ability, string IconUrl);

public sealed record BuildStatsView(
    int Frequency,
    int Games,
    double PickPct,
    double? WinPct,
    string BoughtAt
);
