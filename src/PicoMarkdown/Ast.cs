namespace PicoMarkdown;

public abstract record AstNode
{
    public int StartLine { get; init; }
    public int EndLine { get; init; }
}

public abstract record BlockNode : AstNode;

public abstract record InlineNode : AstNode;

public sealed record Heading(int Level, IReadOnlyList<InlineNode> Inlines) : BlockNode;

public sealed record Paragraph(IReadOnlyList<InlineNode> Inlines) : BlockNode;

public sealed record FencedCode(string Lang, string Code) : BlockNode;

public sealed record List(bool Ordered, int StartNumber, IReadOnlyList<ListItem> Items) : BlockNode;

public sealed record ListItem(IReadOnlyList<BlockNode> Blocks) : BlockNode;

public sealed record BlockQuote(IReadOnlyList<BlockNode> Blocks) : BlockNode;

public sealed record Hr : BlockNode;

public sealed record Table(IReadOnlyList<TableRow> Rows) : BlockNode;

public sealed record TableRow(IReadOnlyList<TableCell> Cells, bool IsHeader) : BlockNode;

public sealed record TableCell(IReadOnlyList<InlineNode> Inlines) : BlockNode;

public sealed record Text(string Value) : InlineNode;

public sealed record Bold(IReadOnlyList<InlineNode> Children) : InlineNode;

public sealed record Italic(IReadOnlyList<InlineNode> Children) : InlineNode;

public sealed record StrikeThrough(IReadOnlyList<InlineNode> Children) : InlineNode;

public sealed record InlineCode(string Code) : InlineNode;

public sealed record Link(string Url, string? Title, IReadOnlyList<InlineNode> Children)
    : InlineNode;

public sealed record Image(string Url, string Alt) : InlineNode;

public sealed record SoftBreak : InlineNode;

public sealed record HardBreak : InlineNode;
