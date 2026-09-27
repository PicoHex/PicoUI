namespace PicoTui.Tests.Layout;

/// <summary>Overlay composition must survive viewports whose lines are shorter
/// than the requested width and viewports shorter than the overlay.</summary>
public sealed class OverlayBoundsTests
{
    [Test]
    public async Task ShortViewportLines_DoNotThrow_AndArePadded()
    {
        var overlay = new Overlay(new Text("X"), new OverlayOptions());

        var composed = overlay.RenderComposed(["short", "x"], 8, 2);

        await Assert.That(composed.Length).IsEqualTo(2);
        await Assert.That(composed[0].Length).IsEqualTo(8);
        await Assert.That(composed[1].Length).IsEqualTo(8);
        await Assert.That(string.Concat(composed)).Contains("X");
    }

    [Test]
    public async Task OverlayTallerThanViewport_ClipsInsteadOfThrowing()
    {
        var overlay = new Overlay(new Box(), new OverlayOptions());

        var composed = overlay.RenderComposed(["--------", "--------"], 8, 2);

        await Assert.That(composed.Length).IsEqualTo(2);
    }

    private sealed class Box : IComponent
    {
        public string[] Render(int width) => ["╔═╗", "║x║", "╚═╝"];

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }
}
