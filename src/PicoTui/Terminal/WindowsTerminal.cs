namespace PicoTui.Terminal;

public sealed class WindowsTerminal : ITerminal, IDisposable
{
    private static readonly TimeSpan ResizePollInterval = TimeSpan.FromMilliseconds(500);

    private readonly ConsoleModeSession _consoleModes;
    private readonly TerminalGeometry _geometry;
    private readonly bool _runSystemHooks;
    private readonly Func<Action<string>, StdinPump> _pumpFactory;
    private readonly Func<Action, ExitGuard> _exitGuardFactory;

    private Action? _onResize;
    private ExitGuard? _exitGuard;
    private Timer? _resizePoll;
    private StdinPump? _pump;
    private Exception? _readerFault;
    private TerminalFault? _modeFault;
    private bool _started;

    public WindowsTerminal()
        : this(
            Win32ConsoleModeOps.Instance,
            TtyWinsizeProbe.Instance,
            runSystemHooks: true,
            StdinPump.ForConsole
        ) { }

    internal WindowsTerminal(IConsoleModeOps consoleModes, IWinsizeProbe winsize, bool runSystemHooks)
        : this(consoleModes, winsize, runSystemHooks, StdinPump.ForConsole) { }

    /// <param name="pumpFactory">Test seam: builds the stdin reader for
    /// <see cref="Start"/>. Production always passes <see cref="StdinPump.ForConsole"/>.</param>
    /// <param name="exitGuardFactory">Test seam (F10b): see
    /// <see cref="UnixTerminal"/> — a guard whose registrations fail must surface as
    /// <c>ExitGuardInstalled == false</c>, not as a silently useless hook.</param>
    internal WindowsTerminal(
        IConsoleModeOps consoleModes,
        IWinsizeProbe winsize,
        bool runSystemHooks,
        Func<Action<string>, StdinPump> pumpFactory,
        Func<Action, ExitGuard>? exitGuardFactory = null
    )
    {
        _consoleModes = new ConsoleModeSession(consoleModes);
        _geometry = new TerminalGeometry(winsize);
        _runSystemHooks = runSystemHooks;
        _pumpFactory = pumpFactory;
        _exitGuardFactory = exitGuardFactory ?? (restore => new ExitGuard(restore));
        _geometry.Refresh();
    }

    public int Columns => _geometry.Columns;

    public int Rows => _geometry.Rows;

    /// <summary>True while the console modes are switched to VT/raw (04-C4 surface).</summary>
    internal bool IsVtModeActive => _consoleModes.IsActive;

    /// <summary>Number of failed SetConsoleMode calls (04-C4: the return value used to be ignored).</summary>
    internal int SetModeFailures => _consoleModes.SetFailures;

    /// <summary>True when the abnormal-exit restore hook is armed (04-H2 surface).</summary>
    internal bool ExitGuardInstalled => _exitGuard is { IsRegistered: true };

    /// <summary>The stdin reader of the current session (04-H3/H4 wiring surface);
    /// null when <see cref="Start"/> ran without system hooks.</summary>
    internal StdinPump? Pump => _pump;

    /// <summary>The reader task's fault, if it ended abnormally (04-H3: the
    /// observation surface that replaces the discarded Task).</summary>
    internal Exception? ReaderFault => _readerFault;

    /// <summary>
    /// Why the console modes could not be switched for this session (null when they
    /// were). F10b: <see cref="ConsoleModeSession.Enter"/>'s result used to be
    /// discarded, so a console that rejected <c>SetConsoleMode</c> looked healthy while
    /// every key arrived line-buffered and echoed.
    /// </summary>
    public TerminalFault? ModeFault => _modeFault;

    public void Start(Action<string> onInput, Action onResize)
    {
        _onResize = onResize;
        _started = true;

        // Console modes: saved before switching so Stop() can put them back.
        // F10b: the result is checked, recorded and announced — never dropped.
        _modeFault = null;
        if (!_consoleModes.Enter())
        {
            _modeFault = new TerminalFault(
                "console mode (SetConsoleMode)",
                _consoleModes.LastEnterFailure ?? "GetConsoleMode/SetConsoleMode refused"
            );
            WarnDegraded(_modeFault);
        }

        // 04-H2: restore the console modes even when the process dies without ExitAsync.
        _exitGuard = _exitGuardFactory(() => _consoleModes.Restore());
        _exitGuard.Register();

        if (!_runSystemHooks)
            return;

        StartReader(onInput);

        // 04-H5: Windows has no SIGWINCH — poll the window rect (≤2 Hz) so a
        // resized window is re-measured instead of keeping the startup size.
        _resizePoll = new Timer(_ => RefreshGeometry(), null, ResizePollInterval, ResizePollInterval);
    }

    /// <summary>F10b: see <see cref="UnixTerminal"/> — a degraded session must say so.</summary>
    private void WarnDegraded(TerminalFault fault)
    {
        if (!_runSystemHooks)
            return;
        try
        {
            Console.Error.WriteLine(
                $"[PicoTui] {fault} — continuing in degraded mode (input may be echoed)."
            );
        }
        catch
        {
            /* a missing stderr must not break the session */
        }
    }

    /// <summary>
    /// 04-H3 ≡ 06-H2 / 04-H4: same reader as <see cref="UnixTerminal"/> — one stream
    /// per session, EOF ends the loop, one callback per framed input unit instead
    /// of one callback per 4096-byte block.
    /// </summary>
    private void StartReader(Action<string> onInput)
    {
        _pump?.Dispose(); // a restart must not leak the previous session's reader

        var pump = _pumpFactory(onInput);
        _pump = pump;
        pump.Buffer.SetPasteMode(true); // TuiLifecycle.Enter writes ?2004h right after Start
        pump.Start();

        // 04-H3: observe the reader task instead of discarding it (see UnixTerminal).
        _ = pump.Completion.ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                    _readerFault = t.Exception;
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    /// <summary>
    /// Re-queries the console window size and invokes the resize callback when it
    /// changed. Public so a frame loop can drive it explicitly.
    /// </summary>
    public bool RefreshGeometry()
    {
        if (!_geometry.Refresh())
            return false;
        _onResize?.Invoke();
        return true;
    }

    public void Stop()
    {
        _exitGuard?.Dispose();
        _exitGuard = null;

        _resizePoll?.Dispose();
        _resizePoll = null;

        // F10b (04-C2 residual): Stop() — not Dispose() — is the shutdown every host
        // runs, so it must release the reader: cancelling alone left the blocked Read
        // pending on an open stdin and an orphan pump ate the next keystroke.
        var pump = _pump;
        _pump = null;
        if (_started)
            pump?.Dispose();

        // Idempotent: writes the modes saved by Start(). Restore stays the last word
        // on the console state (04-C4); the pump never writes to the console and is
        // cancelled *and* closed above.
        _consoleModes.Restore();
    }

    public void Dispose()
    {
        // Stop() already released the reader (F10b).
        Stop();
    }

    public void Write(string data) => Console.Out.Write(data);

    public void MoveBy(int lines) => Write($"\x1b[{lines}A");

    public void HideCursor() => Write("\x1b[?25l");

    public void ShowCursor() => Write("\x1b[?25h");

    public void ClearLine() => Write("\x1b[2K");

    public void ClearFromCursor() => Write("\x1b[0K");

    public void ClearScreen() => Write("\x1b[2J");

    public void SetTitle(string title) => Write($"\x1b]0;{TitleSanitizer.Sanitize(title)}\x07");

    public Task DrainInputAsync(int maxMs, int idleMs) => Task.Delay(Math.Min(maxMs, 50));
}
