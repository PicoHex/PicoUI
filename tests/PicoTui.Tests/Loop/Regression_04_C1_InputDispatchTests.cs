namespace PicoTui.Tests.Loop;

/// <summary>
/// 04-C1 guard: the loop used to drain its channel and throw the payload away
/// (only <c>Resize</c> had an effect), so no consumer could ever see a
/// keystroke. Every drained event must be observable, and an
/// <see cref="EventKind.Input"/> event must reach the handler with the very
/// same <see cref="UiEvent"/> instance that was posted.
/// </summary>
public sealed class Regression_04_C1_InputDispatchTests
{
    private sealed class Root : IComponent
    {
        public int Renders;
        public int Invalidations;

        public string[] Render(int width)
        {
            Renders++;
            return ["root"];
        }

        public void HandleInput(string seq) { }

        public void Invalidate() => Invalidations++;
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;
            await Task.Delay(10);
        }
        return condition();
    }

    [Test]
    public async Task PostedInput_ReachesTheHandlerWithTheSamePayload()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        loop.SetRoot(new Root());
        loop.SetFrameInterval(TimeSpan.Zero);
        var received = new List<string>();
        loop.OnInput = received.Add;

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        try
        {
            loop.Post(new UiEvent(EventKind.Input, "a"));
            await Assert.That(await WaitUntil(() => received.Count > 0)).IsTrue();
        }
        finally
        {
            cts.Cancel();
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
        }

        await Assert.That(received.Count).IsEqualTo(1);
        await Assert.That(received[0]).IsEqualTo("a");
    }

    [Test]
    public async Task PostedEvent_IsHandedToOnEventAsTheSameObject()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        loop.SetRoot(new Root());
        loop.SetFrameInterval(TimeSpan.Zero);
        var events = new List<UiEvent>();
        loop.OnEvent = events.Add;
        var posted = new UiEvent(EventKind.Input, "x");

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        try
        {
            loop.Post(posted);
            await Assert.That(await WaitUntil(() => events.Count > 0)).IsTrue();
        }
        finally
        {
            cts.Cancel();
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
        }

        await Assert.That(events.Count(e => e.Kind == EventKind.Input)).IsEqualTo(1);
        var input = events.Single(e => e.Kind == EventKind.Input);
        await Assert.That(ReferenceEquals(input, posted)).IsTrue();
        await Assert.That(input.Input).IsEqualTo("x");
    }

    [Test]
    public async Task SeveralInputs_AreDispatchedOnceEachInOrder()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        loop.SetRoot(new Root());
        loop.SetFrameInterval(TimeSpan.Zero);
        var received = new List<string>();
        loop.OnInput = received.Add;

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        try
        {
            foreach (var key in new[] { "a", "b", "c" })
                loop.Post(new UiEvent(EventKind.Input, key));
            await Assert.That(await WaitUntil(() => received.Count >= 3)).IsTrue();
        }
        finally
        {
            cts.Cancel();
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
        }

        await Assert.That(received.Count).IsEqualTo(3);
        await Assert.That(received[0]).IsEqualTo("a");
        await Assert.That(received[1]).IsEqualTo("b");
        await Assert.That(received[2]).IsEqualTo("c");
    }

    [Test]
    public async Task ResizeEvent_StillInvalidatesAndIsNotDispatchedAsInput()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        var root = new Root();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.Zero);
        var received = new List<string>();
        loop.OnInput = received.Add;

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        try
        {
            loop.Post(new UiEvent(EventKind.Resize));
            await Assert.That(await WaitUntil(() => root.Invalidations > 0)).IsTrue();
        }
        finally
        {
            cts.Cancel();
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
        }

        await Assert.That(root.Invalidations).IsGreaterThan(0);
        await Assert.That(received.Count).IsEqualTo(0);
    }

    [Test]
    public async Task InputPostedBeforeTheLoopStarts_IsStillDispatched()
    {
        var vt = new VirtualTerminal(10, 2);
        var loop = new UiLoop(vt);
        loop.SetRoot(new Root());
        loop.SetFrameInterval(TimeSpan.Zero);
        var received = new List<string>();
        loop.OnInput = received.Add;
        loop.Post(new UiEvent(EventKind.Input, "z")); // queued before RunAsync

        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        try
        {
            await Assert.That(await WaitUntil(() => received.Count > 0)).IsTrue();
        }
        finally
        {
            cts.Cancel();
            try
            {
                await run;
            }
            catch (OperationCanceledException) { }
        }

        await Assert.That(received.Count).IsEqualTo(1);
        await Assert.That(received[0]).IsEqualTo("z");
    }
}
