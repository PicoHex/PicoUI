namespace PicoMarkdown.Tests;

/// <summary>
/// Streaming consumers freeze blocks with <c>block.EndLine &lt;= LastStableLine</c>
/// (documented in README). Paragraphs and lists must therefore carry their real
/// source line range — previously both stayed 0, so a live paragraph looked
/// "closed" from the first line on.
/// </summary>
public sealed class SourceLineRangeTests
{
    [Test]
    public async Task Paragraph_RecordsItsSourceLineRange()
    {
        var doc = Markdown.Parse("a\nb\n\nc"); // paragraph lines 0-1, paragraph line 3

        var first = (Paragraph)doc.Blocks[0];
        await Assert.That(first.StartLine).IsEqualTo(0);
        await Assert.That(first.EndLine).IsEqualTo(1);

        var second = (Paragraph)doc.Blocks[1];
        await Assert.That(second.StartLine).IsEqualTo(3);
        await Assert.That(second.EndLine).IsEqualTo(3);
    }

    [Test]
    public async Task List_RecordsItsSourceLineRange()
    {
        var doc = Markdown.Parse("intro\n\n- a\n- b\n\nafter");

        var list = (List)doc.Blocks[1];
        await Assert.That(list.StartLine).IsEqualTo(2);
        await Assert.That(list.EndLine).IsEqualTo(3);
    }

    [Test]
    public async Task ParagraphThenList_SplitsLineRanges()
    {
        var doc = Markdown.Parse("text\n- item");

        var paragraph = (Paragraph)doc.Blocks[0];
        var list = (List)doc.Blocks[1];
        await Assert.That(paragraph.StartLine).IsEqualTo(0);
        await Assert.That(paragraph.EndLine).IsEqualTo(0);
        await Assert.That(list.StartLine).IsEqualTo(1);
        await Assert.That(list.EndLine).IsEqualTo(1);
    }

    [Test]
    public async Task ListItem_RecordsItsOwnRange()
    {
        var doc = Markdown.Parse("- a\n- b"); // items on lines 0 and 1

        var list = (List)doc.Blocks[0];
        await Assert.That(((ListItem)list.Items[0]).StartLine).IsEqualTo(0);
        await Assert.That(((ListItem)list.Items[1]).StartLine).IsEqualTo(1);
    }
}
