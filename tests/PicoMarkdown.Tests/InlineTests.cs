namespace PicoMarkdown.Tests;

public sealed class InlineTests
{
    [Test]
    public async Task Parse_BoldAndItalic_ProducesNestedInlines()
    {
        var inlines = ParseInline("**b** and *i*");

        await Assert.That(inlines[0]).IsTypeOf<Bold>();
        var bold = (Bold)inlines[0];
        await Assert.That(bold.Children.Single()).IsTypeOf<Text>();
        await Assert.That(((Text)bold.Children.Single()).Value).IsEqualTo("b");

        await Assert.That(inlines[1]).IsTypeOf<Text>();
        await Assert.That(((Text)inlines[1]).Value).IsEqualTo(" and ");

        await Assert.That(inlines[2]).IsTypeOf<Italic>();
        var italic = (Italic)inlines[2];
        await Assert.That(((Text)italic.Children.Single()).Value).IsEqualTo("i");
    }

    [Test]
    public async Task Parse_UnmatchedDelimiter_IsLiteralText()
    {
        var inlines = ParseInline("a *b");

        await Assert.That(inlines.Single()).IsTypeOf<Text>();
        await Assert.That(((Text)inlines.Single()).Value).IsEqualTo("a *b");
    }

    private static IReadOnlyList<InlineNode> ParseInline(string text) =>
        ((Paragraph)Markdown.Parse(text).Blocks.Single()).Inlines;

    [Test]
    public async Task Parse_InlineCode_ProducesInlineCode()
    {
        var inlines = ParseInline("use `code` here");

        await Assert.That(inlines[1]).IsTypeOf<InlineCode>();
        await Assert.That(((InlineCode)inlines[1]).Code).IsEqualTo("code");
    }

    [Test]
    public async Task Parse_Link_ProducesLink()
    {
        var inlines = ParseInline("[text](https://x.dev)");

        await Assert.That(inlines.Single()).IsTypeOf<Link>();
        var link = (Link)inlines.Single();
        await Assert.That(link.Url).IsEqualTo("https://x.dev");
        await Assert.That(((Text)link.Children.Single()).Value).IsEqualTo("text");
    }

    [Test]
    public async Task Parse_Image_ProducesImage()
    {
        var inlines = ParseInline("![alt](img.png)");

        await Assert.That(inlines.Single()).IsTypeOf<Image>();
        var img = (Image)inlines.Single();
        await Assert.That(img.Url).IsEqualTo("img.png");
        await Assert.That(img.Alt).IsEqualTo("alt");
    }

    [Test]
    public async Task Parse_Strikethrough_ProducesStrikeThrough()
    {
        var inlines = ParseInline("~~gone~~");

        await Assert.That(inlines.Single()).IsTypeOf<StrikeThrough>();
        var st = (StrikeThrough)inlines.Single();
        await Assert.That(((Text)st.Children.Single()).Value).IsEqualTo("gone");
    }
}
