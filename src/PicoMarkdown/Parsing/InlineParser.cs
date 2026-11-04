namespace PicoMarkdown.Parsing;

internal static class InlineParser
{
    private const int MaxDepth = 16;

    public static IReadOnlyList<InlineNode> Parse(string text) => Parse(text, 0);

    private static IReadOnlyList<InlineNode> Parse(string text, int depth)
    {
        if (depth > MaxDepth)
            return [new Text(text)]; // bomb guard: no deeper nesting

        var nodes = new List<InlineNode>();
        var buffer = new StringBuilder();

        // Linear-time link look-ahead (02-H2). The old code ran an unbounded
        // text.IndexOf(']', start + 1) for every '[' and '[', so an input with no ']' at
        // all cost O(n²). We remember where the last scan started and what it found: any
        // later query at or below that ']' is answered from the cache, and scans never
        // overlap, so the whole parse stays O(n).
        // Chosen over a fixed label-length cap because a cap changes the accepted
        // grammar (it would reject legitimate long link texts) while a cache does not:
        // same input, same AST as before — just without the quadratic term.
        var closeFrom = -1; // position the cached scan started at
        var closeAt = -1; // ']' it found (or -1 for "none to the right")

        // Same strategy for the link destination's closing ')' (F9-3, 02-H2
        // residual). "[a](" repeated is the cheapest quadratic input: no ')' at all
        // meant every '[' re-scanned the whole tail (800 KB ⇒ 2.4 s). The queries
        // (close + 2) only move forward as close does, so this cache makes the pair
        // of scans linear without changing the accepted grammar.
        var parenFrom = -1;
        var parenAt = -1;

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && IsEscapable(text[i + 1]))
            {
                buffer.Append(text[i + 1]);
                i++;
                continue;
            }

            if (text[i] == '\n')
            {
                // hard break: trailing backslash, or two+ trailing spaces (trimmed)
                var trailingSpaces = 0;
                while (buffer.Length > 0 && buffer[^1] == ' ')
                {
                    buffer.Length--;
                    trailingSpaces++;
                }
                var hard = false;
                if (buffer.Length > 0 && buffer[^1] == '\\')
                {
                    // an odd raw run before the newline ends with the hard-break marker;
                    // an even run is made of escaped pairs (content backslashes)
                    var run = 0;
                    for (var j = i - 1; j >= 0 && text[j] == '\\'; j--)
                        run++;
                    if (run % 2 == 1)
                    {
                        buffer.Length--;
                        hard = true;
                    }
                }
                if (!hard && trailingSpaces >= 2)
                {
                    hard = true;
                }
                FlushText(nodes, buffer);
                nodes.Add(hard ? new HardBreak() : new SoftBreak());
                continue;
            }

            if (text[i] == '`')
            {
                var end = text.IndexOf('`', i + 1);
                if (end > i)
                {
                    FlushText(nodes, buffer);
                    nodes.Add(new InlineCode(text[(i + 1)..end]));
                    i = end;
                    continue;
                }
            }

            if (text[i] == '!' && i + 1 < text.Length && text[i + 1] == '[')
            {
                var imageClose = FindNextClose(text, i + 2, ref closeFrom, ref closeAt);
                if (
                    TryParseLinkish(
                        text,
                        i + 1,
                        imageClose,
                        ref parenFrom,
                        ref parenAt,
                        out var image,
                        out var consumed,
                        isImage: true
                    )
                )
                {
                    FlushText(nodes, buffer);
                    nodes.Add(image);
                    i = consumed - 1;
                    continue;
                }
            }

            if (text[i] == '[')
            {
                var linkClose = FindNextClose(text, i + 1, ref closeFrom, ref closeAt);
                if (
                    TryParseLinkish(
                        text,
                        i,
                        linkClose,
                        ref parenFrom,
                        ref parenAt,
                        out var link,
                        out var linkConsumed,
                        isImage: false
                    )
                )
                {
                    FlushText(nodes, buffer);
                    nodes.Add(link);
                    i = linkConsumed - 1;
                    continue;
                }
            }

            if (text[i] == '~' && i + 1 < text.Length && text[i + 1] == '~')
            {
                var end = text.IndexOf("~~", i + 2, StringComparison.Ordinal);
                if (end > i + 2)
                {
                    FlushText(nodes, buffer);
                    nodes.Add(new StrikeThrough(Parse(text[(i + 2)..end], depth + 1)));
                    i = end + 1;
                    continue;
                }
            }

            if (text[i] == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("**", i + 2, StringComparison.Ordinal);
                if (end > i)
                {
                    FlushText(nodes, buffer);
                    nodes.Add(new Bold(Parse(text[(i + 2)..end], depth + 1)));
                    i = end + 1;
                    continue;
                }
            }

            if (text[i] == '*' || text[i] == '_')
            {
                var end = text.IndexOf(text[i], i + 1);
                if (end > i + 1)
                {
                    FlushText(nodes, buffer);
                    nodes.Add(new Italic(Parse(text[(i + 1)..end], depth + 1)));
                    i = end;
                    continue;
                }
            }

            buffer.Append(text[i]);
        }
        FlushText(nodes, buffer);
        return nodes;
    }

    private static bool IsEscapable(char c) => c is '*' or '_' or '`' or '[' or ']' or '~' or '\\';

    /// <summary>
    /// Index of the first <c>]</c> at or after <paramref name="from"/>, or -1. Answers from the
    /// cache whenever the previously located <c>]</c> still lies at or after <paramref name="from"/>;
    /// queries only move forward, so the scans are disjoint and the total cost is linear.
    /// </summary>
    private static int FindNextClose(string text, int from, ref int closeFrom, ref int closeAt)
    {
        if (closeFrom >= 0 && closeFrom <= from && (closeAt < 0 || closeAt >= from))
            return closeAt;
        closeAt = text.IndexOf(']', from);
        closeFrom = from;
        return closeAt;
    }

    /// <summary>
    /// Index of the first <c>)</c> at or after <paramref name="from"/>, or -1 — the
    /// destination half of the same incremental memoisation (F9-3). The previously
    /// located <c>)</c> answers the query while it still lies at or after
    /// <paramref name="from"/>; queries only move forward, so the scans never overlap
    /// and the total cost stays linear.
    /// </summary>
    private static int FindNextParen(string text, int from, ref int parenFrom, ref int parenAt)
    {
        if (parenFrom >= 0 && parenFrom <= from && (parenAt < 0 || parenAt >= from))
            return parenAt;
        parenAt = text.IndexOf(')', from);
        parenFrom = from;
        return parenAt;
    }

    private static bool TryParseLinkish(
        string text,
        int start,
        int close,
        ref int parenFrom,
        ref int parenAt,
        out InlineNode link,
        out int consumed,
        bool isImage
    )
    {
        link = null!;
        consumed = 0;
        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
            return false;
        var label = text[(start + 1)..close];
        var urlEnd = FindNextParen(text, close + 2, ref parenFrom, ref parenAt);
        if (urlEnd < 0)
            return false;
        var (url, title) = SplitTitle(text[(close + 2)..urlEnd]);
        if (url.Length == 0)
            return false;
        link = isImage ? new Image(url, label) : new Link(url, title, [new Text(label)]);
        consumed = urlEnd + 1;
        return true;
    }

    /// <summary>Splits a link destination: <c>url</c>, <c>url "title"</c> or <c>url 'title'</c>.</summary>
    private static (string Url, string? Title) SplitTitle(string inner)
    {
        var trimmed = inner.Trim();
        if (trimmed.Length >= 2)
        {
            var quote = trimmed[^1];
            if (quote is '"' or '\'')
            {
                var open = trimmed.LastIndexOf(quote, trimmed.Length - 2);
                if (open > 0 && trimmed[open - 1] == ' ')
                    return (trimmed[..(open - 1)].Trim(), trimmed[(open + 1)..^1]);
            }
        }
        return (trimmed, null);
    }

    private static void FlushText(List<InlineNode> nodes, StringBuilder buffer)
    {
        if (buffer.Length == 0)
            return;
        nodes.Add(new Text(buffer.ToString()));
        buffer.Clear();
    }
}
