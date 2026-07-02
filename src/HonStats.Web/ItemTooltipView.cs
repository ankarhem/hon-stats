using HonStats.Domain.ReferenceData;

namespace HonStats.Web;

/// <param name="Hover">
/// Render as a plain CSS :hover tooltip instead of a popover. Used inside the
/// match modal: Firefox can't resolve a CSS anchor across top-layer roots
/// (popover ↔ the modal dialog holding its anchor), so in-dialog tooltips must
/// stay in the dialog's own top-layer root.
/// </param>
public sealed record ItemTooltipView(
    Item Item,
    string IconUrl,
    BuildStatsView? Stats = null,
    bool Hover = false
);

public sealed record BuildStatsView(
    int Frequency,
    int Games,
    double PickPct,
    double? WinPct,
    string BoughtAt
);
