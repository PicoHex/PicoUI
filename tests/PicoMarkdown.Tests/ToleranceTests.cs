namespace PicoMarkdown.Tests;

public sealed class ToleranceTests
{
    [Test]
    public async Task Parse_EscapedStar_IsLiteralText()
    {
        var inlines = ((Paragraph)Markdown.Parse("\\*not italic*").Blocks.Single()).Inlines;

        await Assert.That(inlines.Single()).IsTypeOf<Text>();
        await Assert.That(((Text)inlines.Single()).Value).IsEqualTo("*not italic*");
    }

    [Test]
    public async Task Parse_DeeplyNestedMarkup_DoesNotOverflow()
    {
        var deep = new string('*', 100) + "x" + new string('*', 100);
        var doc = Markdown.Parse(deep); // must not throw or blow the stack

        await Assert.That(doc.Blocks).Count().IsEqualTo(1);
    }

    [Test]
    public async Task Parse_DeeplyNestedBlockquote_DoesNotOverflow()
    {
        var deep = string.Concat(Enumerable.Repeat("> ", 200)) + "x";
        var doc = Markdown.Parse(deep); // MaxBlockDepth guard, never overflows

        await Assert.That(doc.Blocks).IsNotNull();
    }

    [Test]
    public async Task Parse_GarbageInput_NeverThrows()
    {
        foreach (var input in new[] { "```", "> > > > > > > > > >", "***", "a [b](c", "![", "~~~" })
        {
            await Assert.That(Markdown.Parse(input).Blocks).IsNotNull();
        }
    }
}
