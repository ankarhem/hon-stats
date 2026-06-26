using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace HonStats.Web.Rendering;

// Renders juvio's HoN markup as safe HTML. Markup grammar:
//   ^X ... ^*       color span (X = single letter; ^* resets). Mapped to .gt-X.
//   {a,b,c,d}       per-level values, rendered "a/b/c/d".
//   \n (literal)    line break (the strings store a literal backslash-n, not a newline).
// All text content is HTML-encoded; only generated span/br tags are raw, so input
// from the gamedata API cannot inject markup.
public static class GameText
{
    public static IHtmlContent Render(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return HtmlString.Empty;

        var sb = new StringBuilder();
        var text = new StringBuilder();
        var spanOpen = false;
        var i = 0;

        void FlushText()
        {
            if (text.Length > 0)
            {
                sb.Append(HtmlEncoder.Default.Encode(text.ToString()));
                text.Clear();
            }
        }

        void CloseSpan()
        {
            if (spanOpen)
            {
                sb.Append("</span>");
                spanOpen = false;
            }
        }

        while (i < raw.Length)
        {
            var c = raw[i];

            if (c == '\\' && i + 1 < raw.Length && raw[i + 1] == 'n')
            {
                FlushText();
                CloseSpan();
                sb.Append("<br>");
                i += 2;
                continue;
            }

            if (c == '^' && i + 1 < raw.Length && (raw[i + 1] == '*' || char.IsLetter(raw[i + 1])))
            {
                var next = raw[i + 1];
                FlushText();
                CloseSpan();
                if (next != '*')
                {
                    sb.Append("<span class=\"gt gt-")
                        .Append(char.ToLowerInvariant(next))
                        .Append("\">");
                    spanOpen = true;
                }
                i += 2;
                continue;
            }

            if (c == '{')
            {
                var end = raw.IndexOf('}', i);
                if (end > i)
                {
                    var inner = raw.Substring(i + 1, end - i - 1);
                    text.Append(
                        inner.Contains(',')
                            ? string.Join("/", inner.Split(',').Select(v => v.Trim()))
                            : inner
                    );
                    i = end + 1;
                    continue;
                }
            }

            text.Append(c);
            i++;
        }

        FlushText();
        CloseSpan();
        return new HtmlString(sb.ToString());
    }
}
