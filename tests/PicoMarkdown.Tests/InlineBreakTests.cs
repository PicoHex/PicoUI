namespace PicoMarkdown.Tests;

/// <summary>
/// Line breaks inside a paragraph are real inline nodes: a plain newline is a
/// <see cref="SoftBreak"/>, two trailing spaces or a trailing backslash is a
/// <see cref="HardBreak"/>. Link titles are parsed too — previously the AST
/// declared these nodes/fields but never produced them.
/// </summary>
public sealed class InlineBreakTests
{
    private const string Bs = "\\";
    private const string Nl = "\n";

    private static IReadOnlyList<InlineNode> ParseInline(string text) =>
        ((Paragraph)Markdown.Parse(text).Blocks.Single()).Inlines;

    [Test]
    public async Task EscapedBackslashBeforeNewline_IsLiteralBackslashPlusSoftBreak()
    {
        // a + \\ (escaped backslash) + newline + b → the backslash is content, the break is soft
        var inlines = ParseInline("a" + Bs + Bs + Nl + "b");

        await Assert
            .That(inlines)
            .IsEquivalentTo(
                new InlineNode[] { new Text("a" + Bs), new SoftBreak(), new Text("b") }
            );
    }

    [Test]
    public async Task TwoEscapedBackslashesBeforeNewline_KeepBothAndStaySoft()
    {
        var inlines = ParseInline("a" + Bs + Bs + Bs + Bs + Nl + "b");

        await Assert
            .That(inlines)
            .IsEquivalentTo(
                new InlineNode[] { new Text("a" + Bs + Bs), new SoftBreak(), new Text("b") }
            );
    }

    [Test]
    public async Task EscapedBackslashThenHardBreakMarker_KeepsOneBackslash()
    {
        // three backslashes: one escaped pair (content) + one hard-break marker
        var inlines = ParseInline("a" + Bs + Bs + Bs + Nl + "b");

        await Assert
            .That(inlines)
            .IsEquivalentTo(
                new InlineNode[] { new Text("a" + Bs), new HardBreak(), new Text("b") }
            );
    }

    [Test]
    public async Task MultiLineParagraph_ProducesSoftBreak()
    {
        var inlines = ParseInline("a\nb");

        await Assert
            .That(inlines)
            .IsEquivalentTo(new InlineNode[] { new Text("a"), new SoftBreak(), new Text("b") });
    }

    [Test]
    public async Task TrailingTwoSpaces_ProducesHardBreak()
    {
        var inlines = ParseInline("a  \nb");

        await Assert
            .That(inlines)
            .IsEquivalentTo(new InlineNode[] { new Text("a"), new HardBreak(), new Text("b") });
    }

    [Test]
    public async Task TrailingBackslash_ProducesHardBreak()
    {
        var inlines = ParseInline("a\\\nb");

        await Assert
            .That(inlines)
            .IsEquivalentTo(new InlineNode[] { new Text("a"), new HardBreak(), new Text("b") });
    }

    [Test]
    public async Task EmphasisSpanningLines_KeepsSoftBreakInside()
    {
        var inlines = ParseInline("**a\nb**");

        var bold = (Bold)inlines.Single();
        await Assert.That(bold.Children[1]).IsTypeOf<SoftBreak>();
        await Assert.That(((Text)bold.Children[0]).Value).IsEqualTo("a");
        await Assert.That(((Text)bold.Children[2]).Value).IsEqualTo("b");
    }

    [Test]
    public async Task LinkWithTitle_ParsesTitle()
    {
        var link = (Link)ParseInline("[x](http://u \"T\")").Single();

        await Assert.That(link.Url).IsEqualTo("http://u");
        await Assert.That(link.Title).IsEqualTo("T");
        await Assert.That(((Text)link.Children.Single()).Value).IsEqualTo("x");
    }

    [Test]
    public async Task LinkWithSingleQuotedTitle_ParsesTitle()
    {
        var link = (Link)ParseInline("[x](http://u 'T')").Single();

        await Assert.That(link.Url).IsEqualTo("http://u");
        await Assert.That(link.Title).IsEqualTo("T");
    }

    [Test]
    public async Task LinkWithoutTitle_TitleStaysNull()
    {
        var link = (Link)ParseInline("[x](http://u)").Single();

        await Assert.That(link.Title).IsNull();
    }
}
