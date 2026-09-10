namespace PicoMarkdown.Tests;

public sealed class ParagraphTests
{
    [Test]
    public async Task Parse_SingleLine_ReturnsParagraphWithText()
    {
        var doc = Markdown.Parse("hello world");

        var p = (Paragraph)doc.Blocks.Single();
        await Assert.That(p.Inlines.Single()).IsEqualTo(new Text("hello world"));
    }

    [Test]
    public async Task Parse_BlankLineSeparatesParagraphs()
    {
        var doc = Markdown.Parse("first\n\nsecond");

        await Assert.That(doc.Blocks).Count().IsEqualTo(2);
        await Assert.That(((Paragraph)doc.Blocks[0]).Inlines.Single()).IsEqualTo(new Text("first"));
        await Assert
            .That(((Paragraph)doc.Blocks[1]).Inlines.Single())
            .IsEqualTo(new Text("second"));
    }

    [Test]
    public async Task Parse_CRLF_TreatsAsLineSeparator()
    {
        var doc = Markdown.Parse("first\r\n\r\nsecond");

        await Assert.That(doc.Blocks).Count().IsEqualTo(2);
        await Assert.That(((Paragraph)doc.Blocks[0]).Inlines.Single()).IsEqualTo(new Text("first"));
    }
}
