namespace PicoTui.Tests.Loop;

public sealed class TuiLifecycleTests
{
    [Test]
    public async Task Enter_AltScreenAndHideCursor()
    {
        var vt = new VirtualTerminal(80, 24);

        TuiLifecycle.Enter(vt, _ => { }, () => { });
        await Assert.That(vt.Output.Contains("\x1b[?1049h")).IsTrue(); // alt screen
        await Assert.That(vt.Output.Contains("\x1b[?25l")).IsTrue(); // hide cursor
    }

    [Test]
    public async Task Enter_ForwardsInputAndResizeHandlers()
    {
        var vt = new VirtualTerminal(80, 24);

        string? receivedInput = null;
        var receivedResize = false;
        TuiLifecycle.Enter(vt, s => receivedInput = s, () => receivedResize = true);

        vt.InjectInput("abc");
        vt.Resize(100, 30);

        await Assert.That(receivedInput).IsEqualTo("abc");
        await Assert.That(receivedResize).IsTrue();
    }

    [Test]
    public async Task Exit_RestoresScreenAndCursor()
    {
        var vt = new VirtualTerminal(80, 24);

        TuiLifecycle.Enter(vt, _ => { }, () => { });
        vt.Output = "";
        await TuiLifecycle.ExitAsync(vt);
        await Assert.That(vt.Output.Contains("\x1b[?1049l")).IsTrue(); // main screen
        await Assert.That(vt.Output.Contains("\x1b[?25h")).IsTrue(); // show cursor
        // protocol disable order: kitty first
        var kitty = vt.Output.IndexOf("\x1b[<u");
        var bpaste = vt.Output.IndexOf("\x1b[?2004l");
        await Assert.That(kitty).IsNotEqualTo(-1);
        await Assert.That(bpaste).IsNotEqualTo(-1);
        await Assert.That(kitty).IsLessThan(bpaste);
    }
}
