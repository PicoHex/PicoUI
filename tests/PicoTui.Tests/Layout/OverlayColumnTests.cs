namespace PicoTui.Tests.Layout;

/// <summary>
/// Overlay composition is column based: an overlay over CJK content must land on
/// the requested display column and keep the viewport's column count (previously
/// it spliced by UTF-16 char index, so wide runes shifted everything).
/// </summary>
public sealed class OverlayColumnTests
{
    private sealed class X : IComponent
    {
        public string[] Render(int width) => ["X"];

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task TopLeftOverlay_OnCjkViewport_KeepsColumnCount()
    {
        var overlay = new Overlay(new X(), new OverlayOptions(Anchor: "top-left"));

        var composed = overlay.RenderComposed(["中文中文"], 8, 1);

        await Assert.That(WidthTable.VisibleWidth(composed[0])).IsEqualTo(8);
        await Assert.That(composed[0].StartsWith("X")).IsTrue();
        await Assert.That(composed[0].Contains("文中文")).IsTrue();
    }

    [Test]
    public async Task BottomRightOverlay_OnCjkViewport_SitsAtLastColumn()
    {
        var overlay = new Overlay(new X(), new OverlayOptions(Anchor: "bottom-right"));

        var composed = overlay.RenderComposed(["中文中文"], 8, 1);

        await Assert.That(WidthTable.VisibleWidth(composed[0])).IsEqualTo(8);
        await Assert.That(composed[0].EndsWith("X")).IsTrue(); // X occupies the last column
    }

    [Test]
    public async Task SpliceAtColumns_DropsRuneStraddlingTheBoundary()
    {
        var line = WidthTable.SpliceAtColumns("中文中文", 7, "X", 1);

        await Assert.That(WidthTable.VisibleWidth(line)).IsEqualTo(8);
        await Assert.That(line).IsEqualTo("中文中 X");
    }
}
