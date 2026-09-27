namespace PicoTui.Tests.Loop;

/// <summary>
/// The loop owns frame commits: it renders the initial frame without waiting
/// for an event, and a throttled state change is still committed when the frame
/// budget elapses (no further event needed).
/// </summary>
public sealed class UiLoopFrameCommitTests
{
    private sealed class StatefulRoot : IComponent
    {
        public int Value;
        public int Renders;
        public int LastRendered = -1;

        public string[] Render(int width)
        {
            Renders++;
            LastRendered = Value;
            return [$"v={Value}"];
        }

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task InitialFrame_RendersWithoutEvents()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        var root = new StatefulRoot();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.Zero);

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException) { }

        await Assert.That(root.Renders).IsGreaterThan(0);
        await Assert.That(vt.Output).Contains("v=0");
    }

    [Test]
    public async Task ThrottledStateChange_IsCommittedWithoutFurtherEvents()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        var root = new StatefulRoot();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(200));

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        root.Value = 1;
        loop.Post(new UiEvent(EventKind.Input, "a"));
        await Task.Delay(100); // inside the frame window
        root.Value = 2;
        loop.Post(new UiEvent(EventKind.Input, "b"));
        await Task.Delay(400); // no further events
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException) { }

        await Assert.That(root.LastRendered).IsEqualTo(2);
        await Assert.That(vt.Output).Contains("v=2");
    }

    [Test]
    public async Task RequestRender_CommitsWithoutEvents()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        var root = new StatefulRoot();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.Zero);

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        await Task.Delay(50);
        root.Value = 7;
        loop.RequestRender(); // state changed outside Post (e.g. streaming text)
        await Task.Delay(100);
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException) { }

        await Assert.That(root.LastRendered).IsEqualTo(7);
    }
}
