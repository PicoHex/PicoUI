namespace PicoMarkdown.Tests;

public sealed class QuoteTests
{
    [Test]
    public async Task Parse_Blockquote_ProducesBlockQuote()
    {
        var doc = Markdown.Parse("> line one\n> line two");

        var quote = (BlockQuote)doc.Blocks.Single();
        var p = (Paragraph)quote.Blocks.Single();
        // two source lines → a soft break node (the renderer still joins with a space)
        await Assert
            .That(p.Inlines)
            .IsEquivalentTo(
                new InlineNode[] { new Text("line one"), new SoftBreak(), new Text("line two") }
            );
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
