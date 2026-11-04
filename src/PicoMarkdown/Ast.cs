namespace PicoMarkdown;

/// <summary>
/// Root of the markdown AST produced by <see cref="Markdown.Parse"/>.
/// </summary>
/// <remarks>
/// <para><b>Contract — every string is raw source text.</b> The <see langword="string"/> values
/// carried by these nodes (<see cref="Text.Value"/>, <see cref="Link.Url"/>,
/// <see cref="Link.Title"/>, <see cref="Image.Url"/>, <see cref="Image.Alt"/>,
/// <see cref="FencedCode.Lang"/>, <see cref="FencedCode.Code"/>, <see cref="InlineCode.Code"/>
/// and the heading/table text runs that contain them) are delivered byte for byte as written in
/// the source: no HTML escaping, no entity decoding, no URL scheme whitelist, no character
/// filtering. Untrusted input is therefore untrusted all the way through the AST.</para>
/// <para><b>Escaping and validation belong to the renderer.</b> Text runs, code and attribute
/// values must go through <see cref="MarkdownHtml.Encode("/>, URLs must go through
/// <see cref="MarkdownUrl.Sanitize"/> before they reach an <c>href</c>/<c>src</c>. The parser
/// never rewrites a node to "make it safe"; doing so would corrupt the source and hide the
/// obligation from the consumer.</para>
/// <para><b>No raw-HTML node.</b> The subset has no <c>HtmlBlock</c>/<c>HtmlInline</c> node, so
/// HTML in the source arrives as <see cref="Text"/> — indistinguishable from user prose. A
/// renderer must never pass a <see cref="Text"/> value through unescaped on the assumption that
/// it "is markup".</para>
/// <para><b>Line numbers</b> are 0-based indexes into the newline-normalized source
/// (<c>\r\n</c> and <c>\r</c> are folded to <c>\n</c> at entry).</para>
/// </remarks>
public abstract record AstNode
{
    public int StartLine { get; init; }
    public int EndLine { get; init; }
}

public abstract record BlockNode : AstNode;

public abstract record InlineNode : AstNode;

public sealed record Heading(int Level, IReadOnlyList<InlineNode> Inlines) : BlockNode;

public sealed record Paragraph(IReadOnlyList<InlineNode> Inlines) : BlockNode;

/// <summary>Fenced code block. <c>Lang</c> and <c>Code</c> are raw source text.</summary>
/// <remarks>Render <c>Code</c> escaped (<see cref="MarkdownHtml.Encode"/>) and <c>Lang</c> as a
/// CSS class value only after encoding it — it is not validated as a language identifier here.</remarks>
public sealed record FencedCode(string Lang, string Code) : BlockNode;

public sealed record List(bool Ordered, int StartNumber, IReadOnlyList<ListItem> Items) : BlockNode;

public sealed record ListItem(IReadOnlyList<BlockNode> Blocks) : BlockNode;

public sealed record BlockQuote(IReadOnlyList<BlockNode> Blocks) : BlockNode;

public sealed record Hr : BlockNode;

public sealed record Table(IReadOnlyList<TableRow> Rows) : BlockNode;

public sealed record TableRow(IReadOnlyList<TableCell> Cells, bool IsHeader) : BlockNode;

public sealed record TableCell(IReadOnlyList<InlineNode> Inlines) : BlockNode;

/// <summary>Literal text — <b>the</b> escape-or-you-have-XSS node.</summary>
/// <remarks><c>Value</c> is raw source text, so it can contain <c>&lt;script&gt;</c>, quotes and
/// control characters; it carries no HTML-ness (the subset has no raw-HTML node). Always render
/// it through <see cref="MarkdownHtml.Encode"/>.</remarks>
public sealed record Text(string Value) : InlineNode;

public sealed record Bold(IReadOnlyList<InlineNode> Children) : InlineNode;

public sealed record Italic(IReadOnlyList<InlineNode> Children) : InlineNode;

public sealed record StrikeThrough(IReadOnlyList<InlineNode> Children) : InlineNode;

public sealed record InlineCode(string Code) : InlineNode;

/// <summary>Inline link. <c>Url</c> and <c>Title</c> are raw source text.</summary>
/// <remarks><c>Url</c> has <b>not</b> been scheme-validated: <c>javascript:</c> and <c>data:</c>
/// pass through, by contract. Pass it through <see cref="MarkdownUrl.Sanitize"/> and encode it
/// (<see cref="MarkdownHtml.Encode"/>) before writing it into <c>href</c>.</remarks>
public sealed record Link(string Url, string? Title, IReadOnlyList<InlineNode> Children)
    : InlineNode;

/// <summary>Image. <c>Url</c> and <c>Alt</c> are raw source text.</summary>
/// <remarks>Same rule as <see cref="Link"/>: sanitize <c>Url</c> before <c>src</c>, encode
/// <c>Alt</c> before the <c>alt</c> attribute.</remarks>
public sealed record Image(string Url, string Alt) : InlineNode;

public sealed record SoftBreak : InlineNode;

public sealed record HardBreak : InlineNode;
