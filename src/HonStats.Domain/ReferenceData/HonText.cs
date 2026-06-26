using System.Text;

namespace HonStats.Domain.ReferenceData;

// Cleans juvio's HoN markup out of entity names for plain-text display and URL
// slugs. Names can carry color codes (^X ... ^*) and literal "\n" line breaks,
// e.g. "Shield of Honor\n^vTier I^*" — these must be stripped before display or
// slugging or the slug produces a 404.
public static class HonText
{
    public static string Strip(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var sb = new StringBuilder(raw.Length);
        var i = 0;
        while (i < raw.Length)
        {
            var c = raw[i];

            if (c == '\\' && i + 1 < raw.Length && raw[i + 1] == 'n')
            {
                sb.Append(' ');
                i += 2;
                continue;
            }

            if (c == '^' && i + 1 < raw.Length && (raw[i + 1] == '*' || char.IsLetter(raw[i + 1])))
            {
                i += 2;
                continue;
            }

            sb.Append(c);
            i++;
        }

        return string.Join(
            ' ',
            sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
    }

    // Names can be "Base Name\n^vTier IV^*" — split on the literal "\n" so the tier
    // can render as a separate subtitle. NameOnly = part before, TierOnly = after.
    public static string NameOnly(string? raw) => Strip(SplitName(raw).Name);

    public static string? TierOnly(string? raw)
    {
        var tier = Strip(SplitName(raw).Tier);
        return string.IsNullOrEmpty(tier) ? null : tier;
    }

    private static (string Name, string? Tier) SplitName(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return (string.Empty, null);
        var idx = raw.IndexOf("\\n", StringComparison.Ordinal);
        return idx < 0 ? (raw, null) : (raw[..idx], raw[(idx + 2)..]);
    }

    public static string Slugify(string? raw)
    {
        var clean = Strip(raw).ToLowerInvariant();
        var sb = new StringBuilder(clean.Length);
        var lastHyphen = false;
        foreach (var c in clean)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                lastHyphen = false;
            }
            else if (!lastHyphen)
            {
                sb.Append('-');
                lastHyphen = true;
            }
        }
        return sb.ToString().Trim('-');
    }
}
