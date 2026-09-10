namespace PicoMarkdown.Tests;

public sealed class EmptyDocumentTests
{
    [Test]
    public async Task Parse_EmptyString_ReturnsEmptyDocument()
    {
        var doc = Markdown.Parse("");

        await Assert.That(doc.Blocks).IsEmpty();
        await Assert.That(doc.LastStableLine).IsEqualTo(0);
    }
}
