namespace PicoTui.Tests.Layout;

/// <summary>
/// Root fix for the per-frame re-parse/re-render cost (which the frame timer
/// would otherwise multiply): the expensive components memoize the parse
/// (source-keyed) and the render (width-keyed).
/// </summary>
public sealed class RenderCacheTests
{
    [Test]
    public async Task MarkdownComponent_RepeatedRenderSameWidth_ParsesOnce()
    {
        var m = new MarkdownComponent("hello **world**");

        _ = m.Render(40);
        _ = m.Render(40);
        _ = m.Render(40);

        await Assert.That(m.ParseCount).IsEqualTo(1);
    }

    [Test]
    public async Task MarkdownComponent_WidthChange_KeepsParseAndWrapsDifferently()
    {
        var m = new MarkdownComponent("alpha beta gamma");

        var wide = m.Render(40);
        var narrow = m.Render(6);

        await Assert.That(m.ParseCount).IsEqualTo(1); // AST is width independent
        await Assert.That(wide.Length).IsEqualTo(1);
        await Assert.That(narrow.Length).IsGreaterThan(1);
    }

    [Test]
    public async Task MarkdownComponent_SetText_ReparsesAndRerenders()
    {
        var m = new MarkdownComponent("one");
        await Assert.That(m.Render(40)[0]).IsEqualTo("one");

        m.SetText("two");
        await Assert.That(m.Render(40)[0]).IsEqualTo("two");
        await Assert.That(m.ParseCount).IsEqualTo(2);
    }

    [Test]
    public async Task MermaidComponent_RepeatedRenderSameWidth_RendersArtOnce()
    {
        var m = new MermaidComponent("flowchart TD\nA --> B");

        _ = m.Render(40);
        _ = m.Render(40);

        await Assert.That(m.ArtRenderCount).IsEqualTo(1);
    }

    [Test]
    public async Task MermaidComponent_WidthChange_RerendersArt()
    {
        var m = new MermaidComponent("flowchart TD\nA --> B");

        _ = m.Render(40);
        _ = m.Render(20);

        await Assert.That(m.ArtRenderCount).IsEqualTo(2);
    }
}
