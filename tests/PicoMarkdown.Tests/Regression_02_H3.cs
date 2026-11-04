namespace PicoMarkdown.Tests;

/// <summary>
/// 02-H3 — the table look-ahead must not run before fence/quote/list/heading
/// recognition: an opening fence line followed by a table separator line is code,
/// not a table header.
/// </summary>
public sealed class Regression_02_H3
{
    [Test]
    public async Task FenceLineFollowedBySeparator_IsFencedCodeNotTable()
    {
        var doc = Markdown.Parse("```\n|---|---|\n```");

        await Assert.That(doc.Blocks.Count(b => b is Table)).IsEqualTo(0);
        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Code).IsEqualTo("|---|---|");
    }

    [Test]
    public async Task RealTable_StillParsesAsTable()
    {
        var doc = Markdown.Parse("| a | b |\n|---|---|\n| 1 | 2 |");

        await Assert.That(doc.Blocks.Count(b => b is FencedCode)).IsEqualTo(0);
        var table = (Table)doc.Blocks.Single();
        await Assert.That(table.Rows).Count().IsEqualTo(2);
    }

    [Test]
    public async Task TableAfterParagraph_StillParsesAsTable()
    {
        var doc = Markdown.Parse("intro\n\n| a | b |\n|---|---|\n| 1 | 2 |");

        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
        await Assert.That(doc.Blocks[1]).IsTypeOf<Table>();
    }

    [Test]
    public async Task TableWithUnpipedRows_StillParsesAsTable()
    {
        var doc = Markdown.Parse("a | b\n|---|---|\n1 | 2");

        var table = (Table)doc.Blocks.Single();
        await Assert.That(table.Rows).Count().IsEqualTo(2);
        await Assert.That(table.Rows[1].Cells[1].Inlines.Single()).IsEqualTo(new Text("2"));
    }

    [Test]
    public async Task QuoteFollowedBySeparator_IsNotATable()
    {
        var doc = Markdown.Parse("> quoted\n|---|");

        await Assert.That(doc.Blocks.Count(b => b is Table)).IsEqualTo(0);
        await Assert.That(doc.Blocks[0]).IsTypeOf<BlockQuote>();
    }
}
