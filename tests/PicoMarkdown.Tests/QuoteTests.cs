namespace PicoMarkdown.Tests;

public sealed class QuoteTests
{
    [Test]
    public async Task Parse_Blockquote_ProducesBlockQuote()
    {
        var doc = Markdown.Parse("> line one\n> line two");

        var quote = (BlockQuote)doc.Blocks.Single();
        var p = (Paragraph)quote.Blocks.Single();
        await Assert.That(p.Inlines.Single()).IsEqualTo(new Text("line one line two"));
    }

    [Test]
    public async Task Parse_NestedQuote_ProducesNestedBlockQuote()
    {
        var doc = Markdown.Parse("> outer\n> > inner");

        var quote = (BlockQuote)doc.Blocks.Single();
        await Assert.That(quote.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(quote.Blocks[1]).IsTypeOf<BlockQuote>();
    }
}
