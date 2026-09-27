namespace PicoTui.Tests.Screen;

/// <summary>
/// The frame timer renders every frame; an unchanged frame must not clone the
/// previous cell buffer (measured ~39 KB of a ~79 KB idle frame before the fix).
/// </summary>
public sealed class ScreenRendererAllocationTests
{
    [Test]
    public async Task UnchangedFrame_DoesNotCloneThePreviousBuffer()
    {
        var vt = new VirtualTerminal(80, 24);
        var root = new Text("static content\nsecond line");
        var sr = new ScreenRenderer(vt);
        sr.Render(root); // first frame → full output
        sr.Render(root); // establish the previous buffer

        var before = GC.GetAllocatedBytesForCurrentThread();
        sr.Render(root); // unchanged frame
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // one fresh 80x24 cell buffer is unavoidable; the previous-buffer clone is not
        await Assert.That(allocated).IsLessThan(60_000);
    }
}
