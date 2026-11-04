namespace PicoMarkdown.Tests;

/// <summary>
/// 02-H4 — <c>LastStableLine</c> is the freeze point (<c>block.EndLine &lt;= LastStableLine</c>).
/// A table that ends at EOF can still grow, so it must NOT advance the freeze point —
/// the same rule the unclosed-fence and trailing-paragraph branches already follow.
/// </summary>
public sealed class Regression_02_H4
{
    [Test]
    public async Task TableEndingAtEof_DoesNotAdvanceLastStableLine()
    {
        var doc = Markdown.Parse("intro\n\n| a |\n|---|---|\n| 1 |");

        var table = (Table)doc.Blocks[1];
        await Assert.That(table.EndLine).IsEqualTo(4);
        await Assert.That(doc.LastStableLine).IsEqualTo(1); // blank line before the table
        await Assert.That(doc.LastStableLine < table.EndLine).IsTrue(); // table not frozen
    }

    [Test]
    public async Task TableClosedByBlankLine_AdvancesLastStableLine()
    {
        var doc = Markdown.Parse("intro\n\n| a |\n|---|---|\n| 1 |\n");

        var table = (Table)doc.Blocks[1];
        await Assert.That(table.EndLine).IsEqualTo(4);
        await Assert.That(doc.LastStableLine >= table.EndLine).IsTrue(); // table is frozen
    }

    [Test]
    public async Task TableTerminatedByAnotherSeparatorLine_AdvancesLastStableLine()
    {
        var doc = Markdown.Parse("| a |\n|---|---|\n| 1 |\n|---|");

        var table = (Table)doc.Blocks[0];
        await Assert.That(table.EndLine).IsEqualTo(2); // stopped at the next separator
        await Assert.That(doc.LastStableLine >= table.EndLine).IsTrue();
    }

    [Test]
    public async Task TableOnlyDocumentAtEof_DoesNotFreezeTheTable()
    {
        var doc = Markdown.Parse("| a |\n|---|---|\n| 1 |");

        var table = (Table)doc.Blocks.Single();
        await Assert.That(table.EndLine).IsEqualTo(2);
        await Assert.That(doc.LastStableLine < table.EndLine).IsTrue();
    }
}
