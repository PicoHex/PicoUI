namespace PicoMarkdown.Tests;

public sealed class ReferenceLinkGuardTests
{
    [Test]
    public async Task ReferenceDefinition_IsPlainText()
    {
        var doc = Markdown.Parse("[ref]: https://x.dev\n\n[text][ref]");

        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(doc.Blocks[1]).IsTypeOf<Paragraph>();
    }

    [Test]
    public async Task ReferenceStyleLink_IsPlainText()
    {
        var inlines = ((Paragraph)Markdown.Parse("[text][ref]").Blocks.Single()).Inlines;

        await Assert.That(inlines.Single()).IsTypeOf<Text>();
        await Assert.That(((Text)inlines.Single()).Value).IsEqualTo("[text][ref]");
    }
}
