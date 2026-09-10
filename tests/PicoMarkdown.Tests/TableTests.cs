namespace PicoMarkdown.Tests;

public sealed class TableTests
{
    [Test]
    public async Task Parse_Table_ProducesTable()
    {
        var source = "| a | b |\n|---|---|\n| 1 | 2 |";
        var doc = Markdown.Parse(source);

        var table = (Table)doc.Blocks.Single();
        await Assert.That(table.Rows).Count().IsEqualTo(2);
        await Assert.That(table.Rows[0].IsHeader).IsTrue();
        await Assert.That(table.Rows[0].Cells).Count().IsEqualTo(2);
        await Assert.That(table.Rows[1].Cells[0].Inlines.Single()).IsEqualTo(new Text("1"));
    }

    [Test]
    public async Task Parse_BadSeparator_FallsBackToParagraph()
    {
        var doc = Markdown.Parse("a | b\nno-separator");

        await Assert.That(doc.Blocks[0]).IsTypeOf<Paragraph>();
    }
}
