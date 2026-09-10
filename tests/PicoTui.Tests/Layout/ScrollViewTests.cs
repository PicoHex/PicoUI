namespace PicoTui.Tests.Layout;

public sealed class ScrollViewTests
{
    private sealed class ManyLines : IComponent
    {
        private readonly int _n;

        public ManyLines(int n) => _n = n;

        public string[] Render(int width) =>
            Enumerable.Range(0, _n).Select(i => $"line{i}").ToArray();

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task Viewport_SlicesChild()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions());
        var view = sv.RenderViewport(5, 3); // content height 10, viewport 3
        await Assert.That(view).Count().IsEqualTo(3);
        await Assert.That(view[0]).IsEqualTo("line0");
    }

    [Test]
    public async Task ScrollBy_ShiftsViewport()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions());
        sv.ScrollBy(2);
        var view = sv.RenderViewport(5, 3);
        await Assert.That(view[0]).IsEqualTo("line2");
    }

    [Test]
    public async Task FollowEnd_PinsToBottom()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions(FollowEnd: true));
        var view = sv.RenderViewport(5, 3);
        await Assert.That(view[^1]).IsEqualTo("line9"); // pinned to bottom
    }

    [Test]
    public async Task UnboundedRender_ReturnsFullContentWithoutPadding()
    {
        var sv = new ScrollView(new ManyLines(10), new ScrollOptions());
        var view = sv.Render(5); // unbounded content height
        await Assert.That(view).Count().IsEqualTo(10);
        await Assert.That(view[0]).IsEqualTo("line0");
        await Assert.That(view[^1]).IsEqualTo("line9");
    }
}
