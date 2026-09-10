namespace PicoMarkdown.Tests;

public sealed class HrTests
{
    [Test]
    public async Task Parse_Dashes_ProducesHr()
    {
        var doc = Markdown.Parse("a\n\n---\n\nb");

        await Assert.That(doc.Blocks[1]).IsTypeOf<Hr>();
    }

    [Test]
    public async Task Parse_StarsWithSpaces_ProducesHr()
    {
        var doc = Markdown.Parse("* * *");

        await Assert.That(doc.Blocks.Single()).IsTypeOf<Hr>();
    }
}
