namespace PicoMarkdown.Tests;

public sealed class HeadingTests
{
    [Test]
    public async Task Parse_HashPrefix_ReturnsHeading()
    {
        var doc = Markdown.Parse("# Title\n\n## Sub");

        var h1 = (Heading)doc.Blocks[0];
        await Assert.That(h1.Level).IsEqualTo(1);
        await Assert.That(h1.Inlines.Single()).IsEqualTo(new Text("Title"));

        var h2 = (Heading)doc.Blocks[1];
        await Assert.That(h2.Level).IsEqualTo(2);
    }

    [Test]
    public async Task Parse_HashWithoutSpace_IsParagraph()
    {
        var doc = Markdown.Parse("#notheading");

        await Assert.That(doc.Blocks.Single()).IsTypeOf<Paragraph>();
    }
}
