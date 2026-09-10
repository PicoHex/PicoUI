namespace PicoTui.Tests.Loop;

public sealed class UiLoopTests
{
    private sealed class RecordingRoot : IComponent
    {
        public int Renders;

        public string[] Render(int width)
        {
            Renders++;
            return [""];
        }

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task BurstEvents_ThrottledRenderCount()
    {
        var vt = new VirtualTerminal(80, 24);
        var loop = new UiLoop(vt);
        var root = new RecordingRoot();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(50));
        using var cts = new CancellationTokenSource();
        var task = loop.RunAsync(cts.Token);

        for (var i = 0; i < 100; i++)
            loop.Post(new UiEvent(EventKind.Tick, Tick: i));

        await Task.Delay(300);
        cts.Cancel();
        await task;

        // 100 events coalesced into a few frames (~30fps → ≤6 frames in 300ms)
        await Assert.That(root.Renders).IsLessThan(20);
        await Assert.That(root.Renders).IsGreaterThan(0);
    }

    [Test]
    public async Task Render_GoesThroughDiffPipeline()
    {
        var vt = new VirtualTerminal(10, 3);
        var loop = new UiLoop(vt);
        loop.SetRoot(new Text("abc\ndef\nghi"));
        loop.SetFrameInterval(TimeSpan.Zero);
        using var cts = new CancellationTokenSource();
        var task = loop.RunAsync(cts.Token);

        loop.Post(new UiEvent(EventKind.Tick));
        await Task.Delay(50);
        var afterFirst = vt.Output.Length;
        await Assert.That(afterFirst).IsGreaterThan(0);

        loop.Post(new UiEvent(EventKind.Tick));
        await Task.Delay(50);
        cts.Cancel();
        await task;

        // unchanged frame → no additional output
        await Assert.That(vt.Output.Length).IsEqualTo(afterFirst);
    }
}
