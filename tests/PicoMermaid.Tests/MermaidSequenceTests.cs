namespace PicoMermaid.Tests;

public sealed class MermaidSequenceTests
{
    [Test]
    public async Task SequenceDiagram_RendersRows()
    {
        var art = Mermaid.Render("sequenceDiagram\n  A->>B: hi", 40);
        await Assert.That(art.Warnings).IsEmpty();
        await Assert.That(art.Rows.Length).IsGreaterThan(0);
        await Assert.That(string.Join("\n", art.Rows).Contains("hi")).IsTrue();
    }
}
