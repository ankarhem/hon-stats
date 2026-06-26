namespace HonStats.Web.Rendering;

public static class TimeDisplay
{
    public static string Relative(DateTimeOffset? date)
    {
        if (date is null)
            return "—";

        var diff = DateTimeOffset.UtcNow - date.Value;

        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalHours < 1)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalDays < 1)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 30)
            return $"{(int)diff.TotalDays}d ago";

        return date.Value.ToString("yyyy-MM-dd");
    }
}
