namespace PicoMermaid.Tests;

/// <summary>
/// Width is caller-supplied; 0/negative values must degrade to the fallback
/// (never throw) and truncation must not split a UTF-16 surrogate pair.
/// </summary>
public sealed class WidthBoundaryTests
{
    [Test]
    public async Task NegativeWidth_DoesNotThrow()
    {
        var art = Mermaid.Render("flowchart TD\nA --> B", -1);

        await Assert.That(art.Rows.Length).IsGreaterThan(0);
        await Assert.That(art.Rows.Any(r => r.Contains('\uFFFD'))).IsFalse();
    }

    [Test]
    public async Task ZeroWidth_DoesNotThrow()
    {
        var art = Mermaid.Render("flowchart TD\nA --> B", 0);

        await Assert.That(art.Rows.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Fallback_TruncatesToWidth()
    {
        var art = Mermaid.Render("not a diagram\nabcdef", 3);

        await Assert.That(art.Rows[1]).IsEqualTo("abc");
    }

    [Test]
    public async Task Fallback_DoesNotSplitSurrogatePairs()
    {
        var art = Mermaid.Render("not a diagram\nab😀cd", 3);

        foreach (var row in art.Rows)
        {
            for (var i = 0; i < row.Length; i++)
                if (char.IsHighSurrogate(row[i]))
                    await Assert
                        .That(i + 1 < row.Length && char.IsLowSurrogate(row[i + 1]))
                        .IsTrue();
        }
        await Assert.That(art.Rows[1]).IsEqualTo("ab");
    }
}
