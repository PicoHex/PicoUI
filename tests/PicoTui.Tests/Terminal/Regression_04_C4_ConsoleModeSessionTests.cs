namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-C4 (Critical): the console session must save the original in/out
/// modes, clear <c>ENABLE_LINE_INPUT</c>/<c>ENABLE_ECHO_INPUT</c> on the input handle,
/// notice a failing <c>SetConsoleMode</c>, and put both modes back on Stop().
/// Uses a fake console surface, so it runs on every platform.
/// </summary>
public sealed class Regression_04_C4_ConsoleModeSessionTests
{
    private const uint ProcessedInput = 0x1;
    private const uint LineInput = 0x2;
    private const uint EchoInput = 0x4;
    private const uint VirtualTerminalInput = 0x200;

    [Test]
    public async Task Enter_ClearsLineAndEchoOnTheInputHandle()
    {
        var ops = new FakeConsoleModeOps();
        var session = new ConsoleModeSession(ops);

        await Assert.That(session.Enter()).IsTrue();

        var input = ops.Get(ConsoleApiNative.StdInputHandle);
        await Assert.That(input & LineInput).IsEqualTo(0u);
        await Assert.That(input & EchoInput).IsEqualTo(0u);
        await Assert.That(input & VirtualTerminalInput).IsEqualTo(VirtualTerminalInput);

        var output = ops.Get(ConsoleApiNative.StdOutputHandle);
        await Assert.That(output & ConsoleApiNative.EnableVirtualTerminalProcessing).IsNotEqualTo(0u);
        await Assert.That(session.IsActive).IsTrue();
    }

    [Test]
    public async Task Stop_RestoresInAndOutModes()
    {
        var ops = new FakeConsoleModeOps();
        var originalIn = ops.Get(ConsoleApiNative.StdInputHandle);
        var originalOut = ops.Get(ConsoleApiNative.StdOutputHandle);
        var session = new ConsoleModeSession(ops);

        session.Enter();
        await Assert.That(session.Restore()).IsTrue();

        await Assert.That(ops.Get(ConsoleApiNative.StdInputHandle)).IsEqualTo(originalIn);
        await Assert.That(ops.Get(ConsoleApiNative.StdOutputHandle)).IsEqualTo(originalOut);
        await Assert.That(session.IsActive).IsFalse();

        // idempotent: repeated Stop() neither throws nor re-writes
        var writes = ops.SetCount;
        await Assert.That(session.Restore()).IsTrue();
        await Assert.That(ops.SetCount).IsEqualTo(writes);
    }

    [Test]
    public async Task Enter_ReportsASetConsoleModeFailure()
    {
        var ops = new FakeConsoleModeOps { FailSet = true };
        var session = new ConsoleModeSession(ops);

        await Assert.That(session.Enter()).IsFalse();
        await Assert.That(session.SetFailures).IsGreaterThan(0);
    }

    [Test]
    public async Task WindowsTerminal_StartStop_RestoresConsoleModes()
    {
        var ops = new FakeConsoleModeOps();
        var terminal = new WindowsTerminal(ops, new FakeWinsizeProbe(80, 24), runSystemHooks: false);
        var originalIn = ops.Get(ConsoleApiNative.StdInputHandle);

        terminal.Start(_ => { }, () => { });
        await Assert.That(terminal.IsVtModeActive).IsTrue();
        await Assert.That(ops.Get(ConsoleApiNative.StdInputHandle) & LineInput).IsEqualTo(0u);

        terminal.Stop();
        await Assert.That(terminal.IsVtModeActive).IsFalse();
        await Assert.That(ops.Get(ConsoleApiNative.StdInputHandle)).IsEqualTo(originalIn);
        await Assert.That(terminal.SetModeFailures).IsEqualTo(0);

        terminal.Stop(); // repeated Stop() must not throw
    }

    [Test]
    public async Task VisibleSize_UsesTheWindowRectNotTheScrollbackBuffer()
    {
        // 04-H5: default Windows console has a 9001-row scrollback buffer; the painted
        // area is the Window rect. Reading Size produced a 9001-row frame.
        var info = new ConsoleApiNative.ConsoleScreenBufferInfo
        {
            Size = new ConsoleApiNative.Coord { X = 120, Y = 9001 },
            Window = new ConsoleApiNative.SmallRect
            {
                Left = 0,
                Top = 0,
                Right = 119,
                Bottom = 39,
            },
        };

        var (columns, rows) = ConsoleApiNative.VisibleSize(in info);
        await Assert.That(columns).IsEqualTo(120);
        await Assert.That(rows).IsEqualTo(40);
    }
}

/// <summary>In-memory console mode store keyed by std handle.</summary>
internal sealed class FakeConsoleModeOps : IConsoleModeOps
{
    private readonly Dictionary<int, uint> _modes = new()
    {
        [ConsoleApiNative.StdOutputHandle] = 0x3, // ENABLE_PROCESSED_OUTPUT|ENABLE_WRAP_AT_EOL_OUTPUT
        [ConsoleApiNative.StdInputHandle] = 0x1 | 0x2 | 0x4, // PROCESSED|LINE|ECHO
    };

    internal bool FailSet { get; set; }

    internal int SetCount { get; private set; }

    internal uint Get(int stdHandle) => _modes[stdHandle];

    public bool TryGet(int stdHandle, out uint mode) => _modes.TryGetValue(stdHandle, out mode);

    public bool TrySet(int stdHandle, uint mode)
    {
        SetCount++;
        if (FailSet)
            return false;
        _modes[stdHandle] = mode;
        return true;
    }
}
