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

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && IsEscapable(text[i + 1]))
            {
                buffer.Append(text[i + 1]);
                i++;
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
                if (TryParseLinkish(text, i + 1, out var image, out var consumed, isImage: true))
                {
                    FlushText(nodes, buffer);
                    nodes.Add(image);
                    i = consumed - 1;
                    continue;
                }
            }

            if (
                text[i] == '['
                && TryParseLinkish(text, i, out var link, out var linkConsumed, isImage: false)
            )
            {
                FlushText(nodes, buffer);
                nodes.Add(link);
                i = linkConsumed - 1;
                continue;
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

    private static bool TryParseLinkish(
        string text,
        int start,
        out InlineNode link,
        out int consumed,
        bool isImage
    )
    {
        link = null!;
        consumed = 0;
        var close = text.IndexOf(']', start + 1);
        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
            return false;
        var label = text[(start + 1)..close];
        var urlEnd = text.IndexOf(')', close + 2);
        if (urlEnd < 0)
            return false;
        var url = text[(close + 2)..urlEnd];
        if (url.Length == 0)
            return false;
        link = isImage ? new Image(url, label) : new Link(url, null, [new Text(label)]);
        consumed = urlEnd + 1;
        return true;
    }

    private static void FlushText(List<InlineNode> nodes, StringBuilder buffer)
    {
        if (buffer.Length == 0)
            return;
        nodes.Add(new Text(buffer.ToString()));
        buffer.Clear();
    }
}
