namespace PicoTui.Tests.Loop;

/// <summary>
/// F9-2 guard (04-C1 residual): the loop could dispatch an input event, but
/// nothing in <c>src/</c> ever produced one — every <c>Post(</c> hit in the
/// repository was a definition or a comment. The path a real keystroke takes was
/// therefore dead: <c>StdinPump</c> reads bytes → frames them into one unit per
/// callback → <em>nothing decoded the unit</em> → the loop never saw a key.
/// <para>
/// These guards drive real bytes through the real pump into a fake terminal and
/// assert that the loop thread observes the decoded keys in order, that the
/// production entry point (<see cref="TuiLifecycle.Enter(ITerminal, UiLoop)"/>)
/// is what posts them, and that posting from another thread loses nothing.
/// </para>
/// </summary>
public sealed class Regression_F9_KeyPathTests
{
    /// <summary>Terminal double: records writes, exposes the injected callbacks
    /// so a test can feed it from a foreign thread (the stdin reader's thread).</summary>
    private sealed class FakeTerminal : ITerminal
    {
        private readonly StringBuilder _output = new();
        private Action<string>? _onInput;
        private Action? _onResize;

        public int Columns => 20;

        public int Rows => 3;

        public string Output
        {
            get
            {
                lock (_output)
                    return _output.ToString();
            }
        }

        public void Start(Action<string> onInput, Action onResize)
        {
            _onInput = onInput;
            _onResize = onResize;
        }

        /// <summary>Delivers one framed input unit (what StdinPump would hand the
        /// terminal's <c>onInput</c>, i.e. from the reader thread).</summary>
        public void Inject(string unit) => _onInput?.Invoke(unit);

        public void FireResize() => _onResize?.Invoke();

        public void Stop() { }

        public void Write(string data)
        {
            lock (_output)
                _output.Append(data);
        }

        public void MoveBy(int lines) => Write($"\x1b[{lines}A");

        public void HideCursor() => Write("\x1b[?25l");

        public void ShowCursor() => Write("\x1b[?25h");

        public void ClearLine() => Write("\x1b[2K");

        public void ClearFromCursor() => Write("\x1b[0K");

        public void ClearScreen() => Write("\x1b[2J");

        public void SetTitle(string title) { }

        public Task DrainInputAsync(int maxMs, int idleMs) => Task.CompletedTask;
    }

    private sealed class Root : IComponent
    {
        public int Invalidations;

        public string[] Render(int width) => ["root"];

        public void HandleInput(string seq) { }

        public void Invalidate() { Interlocked.Increment(ref Invalidations); }
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;
            await Task.Delay(5);
        }
        return condition();
    }

    private static async Task RunLoop(UiLoop loop, Func<Task> body)
    {
        using var cts = new CancellationTokenSource();
        var run = loop.RunAsync(cts.Token);
        try
        {
            await body();
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
    }

    [Test]
    public async Task RealBytesFromThePump_ArriveAsDecodedKeysInOrder()
    {
        // the bytes a user produces: 'a', Up arrow, Enter
        var bytes = Encoding.UTF8.GetBytes("a\x1b[A\r");
        var vt = new FakeTerminal();
        var loop = new UiLoop(vt);
        var root = new Root();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(5));

        var keys = new List<Key>();
        var inputs = new List<string>();
        var gate = new object();
        loop.OnKey = k =>
        {
            lock (gate)
                keys.Add(k);
        };
        loop.OnInput = s =>
        {
            lock (gate)
                inputs.Add(s);
        };

        await RunLoop(
            loop,
            async () =>
            {
                TuiLifecycle.Enter(vt, loop); // production wiring: bytes → Key → Post
                using var pump = new StdinPump(new MemoryStream(bytes), vt.Inject);
                pump.Start();
                await Assert.That(await WaitUntil(() => Count(gate, keys) >= 3)).IsTrue();
            }
        );

        Key[] observed;
        string[] units;
        lock (gate)
        {
            observed = [.. keys];
            units = [.. inputs];
        }

        await Assert.That(observed.Length).IsEqualTo(3);
        await Assert.That(KeySpec.Matches(observed[0], "a")).IsTrue();
        await Assert.That(observed[1].IsArrowUp).IsTrue();
        await Assert.That(observed[2].Char).IsEqualTo(Key.Enter.Char);
        // the raw units still reach OnInput, unchanged (04-C1 contract)
        await Assert.That(units.Length).IsEqualTo(3);
        await Assert.That(units[0]).IsEqualTo("a");
        await Assert.That(units[1]).IsEqualTo("\x1b[A");
        await Assert.That(units[2]).IsEqualTo("\r");
    }

    private static int Count(object gate, List<Key> keys)
    {
        lock (gate)
            return keys.Count;
    }

    [Test]
    public async Task EnterOverload_PostsTheKeyIntoTheLoop()
    {
        var vt = new FakeTerminal();
        var loop = new UiLoop(vt);
        loop.SetRoot(new Root());
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(5));
        var keys = new List<Key>();
        loop.OnKey = keys.Add;

        await RunLoop(
            loop,
            async () =>
            {
                TuiLifecycle.Enter(vt, loop);
                // the reader thread pushes a unit; the loop thread must see the key
                vt.Inject("j");
                await Assert.That(await WaitUntil(() => keys.Count == 1)).IsTrue();
            }
        );

        await Assert.That(keys.Count).IsEqualTo(1);
        await Assert.That(keys[0].Char).IsEqualTo('j');
    }

    [Test]
    public async Task UndecodableSequence_StillReachesOnInputAndDoesNotKillTheLoop()
    {
        var vt = new FakeTerminal();
        var loop = new UiLoop(vt);
        loop.SetRoot(new Root());
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(5));
        var keys = new List<Key>();
        var inputs = new List<string>();
        loop.OnKey = keys.Add;
        loop.OnInput = inputs.Add;

        await RunLoop(
            loop,
            async () =>
            {
                TuiLifecycle.Enter(vt, loop);
                vt.Inject("\x1b[1;5H"); // CSI H: not decoded by KeyDecoder
                vt.Inject("k");
                await Assert.That(await WaitUntil(() => inputs.Count >= 2)).IsTrue();
            }
        );

        await Assert.That(inputs).Contains("\x1b[1;5H");
        // an undecodable unit has no key of its own, but it is passed through (never
        // silently dropped) and the following key still arrives
        await Assert.That(keys.Count).IsEqualTo(1);
        await Assert.That(keys[0].Char).IsEqualTo('k');
    }

    [Test]
    public async Task PostingFromForeignThreads_LosesNothing()
    {
        var vt = new FakeTerminal();
        var loop = new UiLoop(vt);
        loop.SetRoot(new Root());
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(5));
        var seen = 0;
        loop.OnKey = _ => Interlocked.Increment(ref seen);

        await RunLoop(
            loop,
            async () =>
            {
                const int perThread = 120;
                var producers = new List<Task>();
                for (var t = 0; t < 2; t++)
                {
                    var offset = t;
                    producers.Add(
                        Task.Run(() =>
                        {
                            for (var i = 0; i < perThread; i++)
                                loop.Post(
                                    new UiEvent(EventKind.Input, Input: "x", Key: new Key('x'))
                                );
                        })
                    );
                }
                await Task.WhenAll(producers);
                await Assert
                    .That(await WaitUntil(() => Volatile.Read(ref seen) >= perThread * 2))
                    .IsTrue();
            }
        );

        await Assert.That(Volatile.Read(ref seen)).IsEqualTo(240);
    }

    [Test]
    public async Task TerminalResize_ReachesTheRootThroughTheSameWiring()
    {
        var vt = new FakeTerminal();
        var loop = new UiLoop(vt);
        var root = new Root();
        loop.SetRoot(root);
        loop.SetFrameInterval(TimeSpan.FromMilliseconds(5));

        await RunLoop(
            loop,
            async () =>
            {
                TuiLifecycle.Enter(vt, loop);
                vt.FireResize();
                await Assert.That(await WaitUntil(() => Volatile.Read(ref root.Invalidations) > 0)).IsTrue();
            }
        );
    }
}
