namespace PicoTui.Tests.Layout;

public sealed class OverlayTests
{
    private sealed class Box : IComponent
    {
        public string[] Render(int width) => ["╔═╗", "║x║", "╚═╝"];

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task CenterAnchor_PlacesOverlayInMiddle()
    {
        var viewport = new[] { "--------", "--------", "--------" };
        var overlay = new Overlay(new Box(), new OverlayOptions(Anchor: "center"));
        var composed = overlay.RenderComposed(viewport, 8, 3);
        await Assert.That(composed[0].Contains("╔")).IsTrue(); // top row has overlay
    }

    [Test]
    public async Task BottomRight_AnchorsThere()
    {
        var viewport = new[] { "--------", "--------", "--------" };
        var overlay = new Overlay(new Box(), new OverlayOptions(Anchor: "bottom-right"));
        var composed = overlay.RenderComposed(viewport, 8, 3);
        await Assert.That(composed[2].Contains("╚")).IsTrue(); // bottom row
    }
}
