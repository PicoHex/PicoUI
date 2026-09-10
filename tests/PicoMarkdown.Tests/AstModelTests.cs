namespace PicoMarkdown.Tests;

public sealed class AstModelTests
{
    [Test]
    public async Task Nodes_ConstructAndPatternMatch()
    {
        AstNode node = new Paragraph([new Text("hi")]);

        var matched = node switch
        {
            Paragraph { Inlines: [Text { Value: "hi" }] } => true,
            _ => false,
        };

        await Assert.That(matched).IsTrue();
        await Assert.That(node is BlockNode).IsTrue();
        await Assert.That(node is InlineNode).IsFalse();
    }
}
