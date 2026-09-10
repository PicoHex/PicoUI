namespace PicoMermaid.Tests;

public sealed class MermaidFlowchartTests
{
    [Test]
    public async Task SimpleEdge_RendersBoxArt()
    {
        var art = Mermaid.Render("flowchart LR\n  A --> B", 40);
        await Assert.That(art.Warnings).IsEmpty();
        await Assert.That(art.Rows.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task UnknownDiagramType_FallsBack()
    {
        var art = Mermaid.Render("classDiagram\n  A --> B", 40);
        await Assert.That(art.Warnings).Count().IsGreaterThan(0);
        await Assert.That(art.Rows.Length).IsGreaterThan(0); // raw code fallback
    }

    [Test]
    public async Task TooWide_FallsBack()
    {
        var art = Mermaid.Render("flowchart LR\n  A --> B", 5);
        await Assert.That(art.Warnings).Count().IsGreaterThan(0);
    }
}
