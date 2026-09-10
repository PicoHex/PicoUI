namespace PicoMarkdown.Tests;

public sealed class FixtureTests
{
    [Test]
    public async Task FullDocument_ParsesWholeAst()
    {
        var src =
            "# Title\n\nSome **bold** and `code`.\n\n- a\n- b\n\n```cs\nint x;\n```\n\n> quote";
        var doc = Markdown.Parse(src);

        await Assert.That(doc.Blocks).Count().IsEqualTo(5); // heading, para, list, code, quote
        await Assert.That(doc.Blocks[0]).IsTypeOf<Heading>();
        await Assert.That(doc.Blocks[1]).IsTypeOf<Paragraph>();
        await Assert.That(doc.Blocks[2]).IsTypeOf<List>();
        await Assert.That(doc.Blocks[3]).IsTypeOf<FencedCode>();
        await Assert.That(doc.Blocks[4]).IsTypeOf<BlockQuote>();
    }

    [Test]
    public async Task Parse_IsPure_SameInputSameAst()
    {
        const string src = "# t\n\ntext";
        var a = Markdown.Parse(src);
        var b = Markdown.Parse(src);

        // structural purity: same block count, same node types, same stable line
        await Assert.That(a.Blocks).Count().IsEqualTo(b.Blocks.Count());
        await Assert.That(a.Blocks[0].GetType()).IsEqualTo(b.Blocks[0].GetType());
        await Assert.That(a.LastStableLine).IsEqualTo(b.LastStableLine);
    }
}
