namespace PicoTui.Tests.Layout;

public sealed class MermaidComponentTests
{
    [Test]
    public async Task Render_Flowchart_ProducesLines()
    {
        var m = new MermaidComponent("flowchart LR\n  A --> B");
        var lines = m.Render(40);
        await Assert.That(lines.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task UnsupportedType_FallsBackToSource()
    {
        var m = new MermaidComponent("classDiagram\n  A --> B");
        var lines = m.Render(40);
        await Assert.That(lines.Length).IsGreaterThan(0); // raw source fallback
    }
}
