namespace PicoMarkdown.Tests;

/// <summary>
/// A paragraph immediately followed by a list (no blank line) must keep source
/// order: the paragraph block precedes the list block. Regression: the list
/// branch consumed rows without flushing the pending paragraph, so the list was
/// emitted first and the paragraph appended after it at EOF / next-block.
/// </summary>
public sealed class BlockOrderTests
{
    [Test]
    public async Task ParagraphThenList_KeepsParagraphFirst()
    {
        var doc = Markdown.Parse("text\n- item");

        await Assert.That(doc.Blocks).Count().IsEqualTo(2);
        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(doc.Blocks[1]).IsTypeOf<List>();
    }

    [Test]
    public async Task ParagraphThenOrderedList_KeepsParagraphFirst()
    {
        var doc = Markdown.Parse("text\n1. one");

        await Assert.That(doc.Blocks).Count().IsEqualTo(2);
        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(((Paragraph)doc.Blocks[0]).Inlines.Single()).IsEqualTo(new Text("text"));
    }

    [Test]
    public async Task ParagraphThenListThenParagraph_KeepsAllThreeInOrder()
    {
        var doc = Markdown.Parse("text\n- item\nnext");

        await Assert.That(doc.Blocks).Count().IsEqualTo(3);
        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(doc.Blocks[1]).IsTypeOf<List>();
        await Assert.That(doc.Blocks[2]).IsTypeOf<Paragraph>();
    }

    [Test]
    public async Task ParagraphThenListThenHeading_KeepsSourceOrder()
    {
        var doc = Markdown.Parse("text\n- item\n# h");

        await Assert.That(doc.Blocks).Count().IsEqualTo(3);
        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(doc.Blocks[1]).IsTypeOf<List>();
        await Assert.That(doc.Blocks[2]).IsTypeOf<Heading>();
    }

    [Test]
    public async Task ParagraphThenNestedList_KeepsParagraphFirst()
    {
        var doc = Markdown.Parse("text\n- a\n  - b\n- c");

        await Assert.That(doc.Blocks).Count().IsEqualTo(2);
        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(doc.Blocks[1]).IsTypeOf<List>();
    }
}
