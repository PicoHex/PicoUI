namespace PicoMarkdown;

public static class Markdown
{
    public static MarkdownDocument Parse(string source)
    {
        source = NormalizeNewlines(source); // CRLF tolerance: \r\n / \r -> \n
        var result = BlockParser.Parse(source);
        return new MarkdownDocument(result.Blocks, result.LastStableLine);
    }

    private static string NormalizeNewlines(string s) =>
        s.Replace("\r\n", "\n").Replace('\r', '\n');
}
