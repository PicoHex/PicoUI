namespace PicoTui.Tests.Screen;

/// <summary>
/// 05-C2 guard: the render entry point must hand the available height to the
/// root component, so a vertical stack can honour Grow/Shrink/Basis and the
/// scroll view can clip to its viewport. Without it the input line and the
/// status bar are pushed off-screen as soon as the conversation exceeds a page.
/// </summary>
public sealed class Regression_05_C2_HeightPlumbingTests
{
    private sealed class Lines(int n, string prefix) : IComponent
    {
        public string[] Render(int width) =>
            Enumerable.Range(0, n).Select(i => $"{prefix}{i}").ToArray();

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    private sealed class Fixed(params string[] lines) : IComponent
    {
        public string[] Render(int width) => lines;

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    /// <summary>Records the height it was offered by the render entry point.</summary>
    private sealed class HeightProbe : IComponent
    {
        public int SeenWidth = -1;
        public int SeenHeight = -1;

        public string[] Render(int width) => ["probe"];

        public string[] Render(int width, int height)
        {
            SeenWidth = width;
            SeenHeight = height;
            return ["probe"];
        }

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    /// <summary>The consumer shape (PicoAgent AppShell): messages grow, editor and
    /// status bar are pinned to one row each.</summary>
    private static VStack BuildAppLikeStack(int contentLines, out StatusBar status)
    {
        var messages = new ScrollView(new Lines(contentLines, "line"), new ScrollOptions(FollowEnd: true));
        var editor = new Fixed("> type here");
        status = new StatusBar();
        status.Set("ready");

        var stack = new VStack();
        stack.Add(new LayoutItem(messages, Grow: 1, MinSize: 1));
        stack.Add(new LayoutItem(editor, Basis: 1, Shrink: 0));
        stack.Add(new LayoutItem(status, Basis: 1, Shrink: 0));
        return stack;
    }

    [Test]
    public async Task ScreenRenderer_HandsTheTerminalHeightToTheRoot()
    {
        var vt = new VirtualTerminal(30, 7);
        var probe = new HeightProbe();

        new ScreenRenderer(vt).Render(probe);

        await Assert.That(probe.SeenWidth).IsEqualTo(30);
        await Assert.That(probe.SeenHeight).IsEqualTo(7);
    }

    [Test]
    public async Task BoundedStack_With100LinesAndHeight24_IsExactly24Rows()
    {
        var stack = BuildAppLikeStack(100, out _);

        var lines = stack.Render(40, 24);

        await Assert.That(lines.Length).IsEqualTo(24); // nothing drawn past the screen
        await Assert.That(lines[22]).Contains("> type here"); // editor row
        await Assert.That(lines[23]).Contains("ready"); // status bar is the last row
    }

    [Test]
    public async Task ScreenRenderer_With100LinesAndHeight24_KeepsEditorAndStatusOnScreen()
    {
        var vt = new VirtualTerminal(40, 24);
        var stack = BuildAppLikeStack(100, out _);

        new ScreenRenderer(vt).Render(stack);

        var rows = vt.Output.Split("\r\n");
        await Assert.That(rows.Length).IsEqualTo(24); // exactly Rows rows, no wrap-around
        await Assert.That(rows[22]).Contains("> type here");
        await Assert.That(rows[23]).Contains("ready"); // last visible row is the status bar
        await Assert.That(vt.Output).Contains("line99"); // the newest message is on screen
    }

    [Test]
    public async Task BoundedStack_PinsFollowingViewportToTheNewestLines()
    {
        var stack = BuildAppLikeStack(100, out _);

        var lines = stack.Render(40, 24);

        await Assert.That(lines[0]).Contains("line78"); // 22-row viewport over 100 lines, follow-end
        await Assert.That(lines[21]).Contains("line99");
    }

    [Test]
    public async Task DefaultRenderOverload_StillFallsBackToWidthOnly()
    {
        // A component that only implements Render(int) keeps working through the
        // height-aware entry point (default interface member).
        var vt = new VirtualTerminal(12, 3);
        var sr = new ScreenRenderer(vt);

        sr.Render(new Fixed("x", "y", "z"));

        await Assert.That(vt.Output).Contains("x");
        await Assert.That(vt.Output).Contains("z");
    }
}
