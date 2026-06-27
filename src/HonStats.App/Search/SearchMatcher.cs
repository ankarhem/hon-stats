using System.Globalization;
using System.Text;

namespace HonStats.App.Search;

// Typo-tolerant search matching for short reference-data names (heroes, items).
//
// A query "matches" a name when, after normalizing both sides, ANY of these holds:
//   1. the normalized name contains the normalized query (substring — catches
//      prefix/suffix/infix and apostrophe-omission like "phoenix" vs "Phoenix's"),
//   2. every query token fuzzy-matches some name token within a length-scaled
//      Levenshtein threshold (catches typos within individual words), or
//   3. the whole normalized strings are within the threshold (catches typos that
//      span token boundaries, mainly for single-word names).
//
// Normalization lowercases, folds diacritics (é→e), glues apostrophes
// ("Phoenix's"→"phoenixs"), and collapses every other non-alphanumeric run to a
// single space. The dataset is tiny (≤~300 names ≤30 chars) so per-keystroke cost
// is sub-millisecond; no caching or SIMD is warranted.
public static class SearchMatcher
{
    /// <summary>
    /// True when <paramref name="query"/> matches <paramref name="name"/> by
    /// normalized substring, token-level fuzzy, or whole-string fuzzy. A null/blank
    /// query matches everything (callers gate dimming on a separate blank check).
    /// </summary>
    public static bool Matches(string? query, string name)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var q = Normalize(query);
        if (q.Length == 0)
            return true;

        var n = Normalize(name);
        if (n.Length == 0)
            return false;

        if (n.Contains(q, StringComparison.Ordinal))
            return true;

        if (TokensMatch(q, n))
            return true;

        return Levenshtein(q, n) <= MaxDistanceFor(q.Length);
    }

    /// <summary>
    /// Scores how well <paramref name="query"/> matches <paramref name="name"/> on
    /// a fixed tiered scale (higher is better; 0 = no match). The first tier that
    /// hits wins, in this priority order:
    /// <list type="bullet">
    ///   <item><term>100</term><description>blank/whitespace query or normalized
    ///     query empty (matches-all sentinel; callers gate blanks), or normalized
    ///     query equals normalized name — the latter catches apostrophe-glued
    ///     exacts ("Phoenix's" vs "phoenixs").</description></item>
    ///   <item><term>90</term><description>normalized name starts with normalized
    ///     query (prefix).</description></item>
    ///   <item><term>80</term><description>normalized name contains normalized
    ///     query (substring / infix).</description></item>
    ///   <item><term>65</term><description>every query token fuzzy-matches a name
    ///     token within the length-scaled edit threshold (token-level typo, e.g.
    ///     "tlaon" vs "Phoenix's Talon").</description></item>
    ///   <item><term>45</term><description>whole normalized strings within the
    ///     length-scaled edit threshold (typo spanning token boundaries, e.g. a
    ///     dropped space).</description></item>
    ///   <item><term>0</term><description>otherwise.</description></item>
    /// </list>
    /// </summary>
    public static int Score(string? query, string name)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 100;

        var q = Normalize(query);
        if (q.Length == 0)
            return 100;

        var n = Normalize(name);

        if (q == n)
            return 100;
        if (n.StartsWith(q, StringComparison.Ordinal))
            return 90;
        if (n.Contains(q, StringComparison.Ordinal))
            return 80;
        if (TokensMatch(q, n))
            return 65;
        if (Levenshtein(q, n) <= MaxDistanceFor(q.Length))
            return 45;
        return 0;
    }

    /// <summary>
    /// Builds a search key: lowercase, diacritics folded, apostrophes glued
    /// ("Phoenix's Talon"→"phoenixs talon"), other non-alphanumeric runs collapsed
    /// to one space, trimmed.
    /// </summary>
    public static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        var lastWasSpace = true; // suppresses a leading space

        // Lowercase-then-decompose. Textbook order is casefold-before-NFKD, but for
        // the BMP ASCII game names here the result is identical.
        foreach (var ch in value.ToLowerInvariant().Normalize(NormalizationForm.FormKD))
        {
            if (char.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue; // drop combining diacritics (é → e + ́)
            if (ch is '\'' or '\u2019' or '\u02BC' or '`' or '\u00B4')
                continue; // apostrophes/quotes → glue (no separator inserted)

            if (!char.IsLetterOrDigit(ch))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
                continue;
            }

            sb.Append(ch);
            lastWasSpace = false;
        }

        if (lastWasSpace && sb.Length > 0)
            sb.Length--; // trim trailing space
        return sb.ToString();
    }

    // Each query token must fuzzy-match some name token (substring or within the
    // length-scaled Levenshtein threshold). Runs for single-token queries too: a
    // short typo like "tlaon" against the multi-token "phoenixs talon" only matches
    // here (substring fails, whole-string fuzzy fails on the length gap).
    private static bool TokensMatch(string query, string name)
    {
        var qTokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var nTokens = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var qt in qTokens)
        {
            var matched = false;
            foreach (var nt in nTokens)
            {
                if (nt.Contains(qt, StringComparison.Ordinal))
                {
                    matched = true;
                    break;
                }
                var max = MaxDistanceFor(qt.Length);
                if (Math.Abs(qt.Length - nt.Length) <= max && Levenshtein(qt, nt) <= max)
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
                return false;
        }
        return true;
    }

    // Length-scaled edit-distance tolerance: too strict to tolerate typos on very
    // short tokens (would over-match), looser as tokens grow.
    private static int MaxDistanceFor(int len) =>
        len switch
        {
            <= 2 => 0,
            <= 4 => 1,
            <= 7 => 2,
            _ => 3,
        };

    // Classic Levenshtein edit distance, single-row (two buffers swapped), stackalloc.
    // Inputs are short (≤~30 chars) so the full matrix is trivially cheap.
    private static int Levenshtein(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.Length == 0)
            return b.Length;
        if (b.Length == 0)
            return a.Length;

        // Keep b the shorter side to minimize buffer width.
        if (a.Length < b.Length)
        {
            var swap = a;
            a = b;
            b = swap;
        }

        Span<int> prev = stackalloc int[b.Length + 1];
        Span<int> curr = stackalloc int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(prev[j] + 1, curr[j - 1] + 1), prev[j - 1] + cost);
            }
            var tmp = prev;
            prev = curr;
            curr = tmp;
        }
        return prev[b.Length];
    }
}
