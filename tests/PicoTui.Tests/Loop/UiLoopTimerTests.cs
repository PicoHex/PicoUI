namespace PicoTui.Tests.Loop;

/// <summary>
/// Root fix for the "state changed but nothing rendered" class of bugs: the loop
/// owns a frame timer, so any state a component reads is committed at the next
/// frame — no Post/RequestRender required. This is what makes the library safe
/// for consumers that mutate state from their own pump.
/// </summary>
public sealed class UiLoopTimerTests
{
    private sealed class StatefulRoot : IComponent
    {
        public int Value;
        public int LastRendered = -1;

        public string[] Render(int width)
        {
            LastRendered = Value;
            return [$"v={Value}"];
        }

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task UnnotifiedStateChange_IsCommittedByTheFrameTimer()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        var root = new StatefulRoot();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(100));

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        await Task.Delay(150); // initial frame committed
        root.Value = 9; // no Post, no RequestRender
        await Task.Delay(250); // the frame timer must pick it up
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException) { }

        await Assert.That(root.LastRendered).IsEqualTo(9);
        await Assert.That(vt.Output).Contains("v=9");
    }

    [Test]
    public async Task ZeroFrameInterval_StaysOnDemand()
    {
        // interval 0 = "no background timer": no event/request → no render (no busy loop)
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        var root = new StatefulRoot();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.Zero);

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        await Task.Delay(120);
        var rendersBefore = vt.Output.Length;
        root.Value = 5;
        await Task.Delay(200);
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException) { }

        await Assert.That(vt.Output.Length).IsEqualTo(rendersBefore);
        await Assert.That(loop.RenderCount).IsEqualTo(1); // only the initial frame
    }
}
