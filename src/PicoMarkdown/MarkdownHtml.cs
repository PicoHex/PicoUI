namespace PicoMarkdown;

/// <summary>
/// HTML encoding helpers for consumers that render the AST as HTML.
/// </summary>
/// <remarks>
/// The AST hands out raw source text (see <see cref="AstNode"/>); nothing in PicoMarkdown
/// escapes it. A renderer that emits HTML strings must send every text run, attribute value and
/// code span through <see cref="Encode"/> — and every URL through
/// <see cref="MarkdownUrl.Sanitize"/> — before writing it into the output document.
/// </remarks>
public static class MarkdownHtml
{
    /// <summary>
    /// Encodes a value for use as HTML text content or inside a double- or single-quoted
    /// attribute value: <c>&amp;</c>, <c>&lt;</c>, <c>&gt;</c>, <c>"</c> and <c>'</c> become
    /// entities, so the result cannot break out of a tag, an attribute or a script context.
    /// </summary>
    /// <param name="value">Raw text; <see langword="null"/> is treated as the empty string.</param>
    /// <returns>The encoded text (never <see langword="null"/>). Ampersands are escaped exactly
    /// once — the input is not entity-decoded first.</returns>
    public static string Encode(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        // fast path: nothing to escape (the overwhelmingly common case)
        var clean = true;
        foreach (var c in value)
        {
            if (NeedsEncoding(c))
            {
                clean = false;
                break;
            }
        }
        if (clean)
            return value;

        var sb = new StringBuilder(value.Length + 16);
        foreach (var c in value)
        {
            switch (c)
            {
                case '&':
                    sb.Append("&amp;");
                    break;
                case '<':
                    sb.Append("&lt;");
                    break;
                case '>':
                    sb.Append("&gt;");
                    break;
                case '"':
                    sb.Append("&quot;");
                    break;
                case '\'':
                    sb.Append("&#39;");
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static bool NeedsEncoding(char c) => c is '&' or '<' or '>' or '"' or '\'';
}
