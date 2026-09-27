namespace PicoTui.Tests.Layout;

/// <summary>Scrolling past the end must clamp to the last page instead of
/// rendering a blank viewport.</summary>
public sealed class ScrollViewClampTests
{
    private sealed class ManyLines(int n) : IComponent
    {
        public string[] Render(int width) =>
            Enumerable.Range(0, n).Select(i => $"line{i}").ToArray();

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task ScrollBy_PastEnd_ClampsToLastPage()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions());
        sv.ScrollBy(99);

        var view = sv.RenderViewport(5, 3);

        await Assert.That(view[0]).IsEqualTo("line7");
        await Assert.That(view[2]).IsEqualTo("line9");
    }

    [Test]
    public async Task ScrollBy_ContentShorterThanViewport_StaysAtTop()
    {
        var sv = new ScrollView(new ManyLines(3), new ScrollOptions());
        sv.ScrollBy(10);

        var view = sv.RenderViewport(5, 4);

        await Assert.That(view[0]).IsEqualTo("line0");
        await Assert.That(view[2]).IsEqualTo("line2");
    }
}
