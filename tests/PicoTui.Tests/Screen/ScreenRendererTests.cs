namespace PicoTui.Tests.Screen;

public sealed class ScreenRendererTests
{
    [Test]
    public async Task FirstRender_WritesFullFrameWrappedInSyncOutput()
    {
        var vt = new VirtualTerminal(10, 3);
        var sr = new ScreenRenderer(vt);
        sr.Render(new Text("abc\ndef\nghi"));

        await Assert.That(vt.Output.Contains("abc")).IsTrue();
        await Assert.That(vt.Output.Contains("def")).IsTrue();
        await Assert.That(vt.Output.Contains("ghi")).IsTrue();
        await Assert.That(vt.Output.Contains("\x1b[?2026h")).IsTrue(); // sync output on
        await Assert.That(vt.Output.Contains("\x1b[?2026l")).IsTrue(); // sync output off
    }

    [Test]
    public async Task UnchangedFrame_EmitsNothing()
    {
        var vt = new VirtualTerminal(10, 3);
        var sr = new ScreenRenderer(vt);
        var root = new Text("abc\ndef\nghi");
        sr.Render(root);
        var len = vt.Output.Length;
        sr.Render(root);
        await Assert.That(vt.Output.Length).IsEqualTo(len);
    }

    [Test]
    public async Task ChangedLine_EmitsPositioningAndClearToEndOnly()
    {
        var vt = new VirtualTerminal(10, 3);
        var sr = new ScreenRenderer(vt);
        sr.Render(new Text("abc\ndef\nghi"));
        vt.Output = "";

        sr.Render(new Text("abc\nXYZ\nghi"));

        await Assert.That(vt.Output.Contains("\x1b[2;1H")).IsTrue(); // row 2, col 1
        await Assert.That(vt.Output.Contains("XYZ")).IsTrue();
        await Assert.That(vt.Output.Contains("\x1b[K")).IsTrue(); // clear to end
        await Assert.That(vt.Output.Contains("abc")).IsFalse(); // unchanged lines not rewritten
        await Assert.That(vt.Output.Contains("ghi")).IsFalse();
    }

    [Test]
    public async Task MostLinesChanged_FullRedraw()
    {
        var vt = new VirtualTerminal(5, 4);
        var sr = new ScreenRenderer(vt);
        sr.Render(new Text("a\nb\nc\nd"));
        vt.Output = "";

        sr.Render(new Text("A\nB\nC\nD")); // 4/4 changed > 50%

        await Assert.That(vt.Output.Contains("\x1b[2J")).IsTrue(); // clear screen
        await Assert.That(vt.Output.Contains("A")).IsTrue();
        await Assert.That(vt.Output.Contains("D")).IsTrue();
    }

    [Test]
    public async Task ShorterContent_ClearsRemainingRow()
    {
        var vt = new VirtualTerminal(10, 10);
        var sr = new ScreenRenderer(vt);
        sr.Render(new Text("abc\ndef\nghi"));
        vt.Output = "";

        sr.Render(new Text("abc\ndef")); // row 3 (index 2) now blank — 1/10 changed

        await Assert.That(vt.Output.Contains("\x1b[3;1H")).IsTrue(); // row 3, col 1
        await Assert.That(vt.Output.Contains("\x1b[K")).IsTrue(); // clear the leftover "ghi"
    }
}
