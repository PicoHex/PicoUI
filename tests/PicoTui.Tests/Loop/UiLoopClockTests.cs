namespace PicoTui.Tests.Loop;

/// <summary>
/// The frame deadline must tolerate a pathological timestamp source: a backwards
/// jump must not push the next frame further out than one frame interval.
/// </summary>
public sealed class UiLoopClockTests
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
    public async Task BackwardsTimestampJump_DoesNotStallFrames()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        var root = new StatefulRoot();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(100));
        var fake = Stopwatch.GetTimestamp();
        loop.TimestampProvider = () => fake; // test-controlled clock

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        await Task.Delay(50); // initial frame
        fake -= Stopwatch.Frequency * 60; // timestamp jumps back one minute
        root.Value = 4;
        await Task.Delay(400); // must still commit within ~one interval
        cts.Cancel();
        try
        {
            await run;
        }
        catch (OperationCanceledException) { }

        await Assert.That(root.LastRendered).IsEqualTo(4);
        await Assert.That(vt.Output).Contains("v=4");
    }
}
