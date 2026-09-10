using MdText = PicoMarkdown.Text;

namespace PicoTui.Layout;

/// <summary>
/// Renders a markdown source string to terminal lines by walking the
/// PicoMarkdown AST. v1 emits structure-preserving plain text (headings with a
/// marker, code preserved verbatim, list/table rows) — ANSI styling is a
/// future refinement over the same AST walk.
/// </summary>
public sealed class MarkdownComponent : IComponent
{
    private string _source = "";

    public MarkdownComponent(string source) => _source = source;

    public void SetText(string source)
    {
        _source = source;
        Invalidate();
    }

    public string[] Render(int width)
    {
        var doc = Markdown.Parse(_source);
        var lines = new List<string>();
        foreach (var block in doc.Blocks)
            RenderBlock(block, lines, width);
        return [.. lines];
    }

    private static void RenderBlock(BlockNode block, List<string> lines, int width)
    {
        switch (block)
        {
            case Paragraph p:
                lines.AddRange(InlineText(p.Inlines, width));
                break;
            case Heading h:
                var prefix = new string('#', h.Level);
                lines.AddRange(InlineText(h.Inlines, width, prefix + " "));
                break;
            case FencedCode c:
                lines.AddRange(c.Code.Split('\n').Select(l => l.Length > width ? l[..width] : l));
                break;
            case List l:
                var n = 0;
                foreach (var item in l.Items)
                {
                    var marker = l.Ordered ? $"{l.StartNumber + n}. " : "- ";
                    foreach (var child in item.Blocks)
                    {
                        if (child is Paragraph p2)
                            lines.AddRange(InlineText(p2.Inlines, width, "  " + marker));
                        else if (child is List nested)
                        {
                            var nestedLines = new List<string>();
                            RenderBlock(nested, nestedLines, width);
                            lines.AddRange(nestedLines.Select(s => "  " + s));
                        }
                        else
                            RenderBlock(child, lines, width);
                    }
                    n++;
                }
                break;
            case BlockQuote q:
                var quoteLines = new List<string>();
                foreach (var child in q.Blocks)
                    RenderBlock(child, quoteLines, width);
                lines.AddRange(quoteLines.Select(s => "> " + s));
                break;
            case Hr:
                lines.Add(new string('─', Math.Min(width, 40)));
                break;
            case Table t:
                foreach (var row in t.Rows)
                {
                    var cells = row.Cells.Select(c =>
                        string.Join("", InlineText(c.Inlines, int.MaxValue))
                    );
                    lines.Add(string.Join(" | ", cells));
                    if (row.IsHeader)
                        lines.Add(string.Join(" | ", row.Cells.Select(_ => new string('─', 3))));
                }
                break;
        }
    }

    private static IEnumerable<string> InlineText(
        IReadOnlyList<InlineNode> inlines,
        int width,
        string prefix = ""
    )
    {
        var sb = new System.Text.StringBuilder(prefix);
        foreach (var node in inlines)
        {
            switch (node)
            {
                case MdText t:
                    sb.Append(t.Value);
                    break;
                case InlineCode c:
                    sb.Append('`').Append(c.Code).Append('`');
                    break;
                case Bold b:
                    sb.Append(Join(b.Children));
                    break;
                case Italic i:
                    sb.Append(Join(i.Children));
                    break;
                case StrikeThrough st:
                    sb.Append(Join(st.Children));
                    break;
                case Link link:
                    sb.Append(Join(link.Children));
                    break;
                case Image img:
                    sb.Append($"[{img.Alt}]");
                    break;
            }
        }
        var text = sb.ToString();
        yield return text.Length > width ? text[..width] : text;
    }

    private static string Join(IReadOnlyList<InlineNode> nodes) =>
        string.Concat(
            nodes.Select(n =>
                n switch
                {
                    MdText t => t.Value,
                    InlineCode c => c.Code,
                    _ => "",
                }
            )
        );

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
