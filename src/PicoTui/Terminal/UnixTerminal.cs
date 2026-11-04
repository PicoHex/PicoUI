namespace PicoTui.Terminal;

public sealed class UnixTerminal : ITerminal, IDisposable
{
    private readonly RawModeSession _rawMode;
    private readonly TerminalGeometry _geometry;
    private readonly bool _runSystemHooks;
    private readonly Func<Action<string>, StdinPump> _pumpFactory;
    private readonly Func<Action, ExitGuard> _exitGuardFactory;

    private Action? _onResize;
    private ExitGuard? _exitGuard;
    private PosixSignalRegistration? _winch;
    private StdinPump? _pump;
    private Exception? _readerFault;
    private TerminalFault? _modeFault;
    private bool _started;

    public UnixTerminal()
        : this(TtyTermiosOps.Instance, TtyWinsizeProbe.Instance, runSystemHooks: true, StdinPump.ForConsole)
    { }

    internal UnixTerminal(ITermiosOps termios, IWinsizeProbe winsize, bool runSystemHooks)
        : this(termios, winsize, runSystemHooks, StdinPump.ForConsole) { }

    /// <param name="pumpFactory">Test seam: builds the stdin reader for
    /// <see cref="Start"/>. Production always passes <see cref="StdinPump.ForConsole"/>.</param>
    /// <param name="exitGuardFactory">Test seam (F10b): builds the abnormal-exit guard.
    /// Production uses the default one; a test injects a guard with a fake signal
    /// registrar / process-exit source to prove that a failed registration is
    /// <em>reported</em> instead of being assumed.</param>
    internal UnixTerminal(
        ITermiosOps termios,
        IWinsizeProbe winsize,
        bool runSystemHooks,
        Func<Action<string>, StdinPump> pumpFactory,
        Func<Action, ExitGuard>? exitGuardFactory = null
    )
    {
        _rawMode = new RawModeSession(termios);
        _geometry = new TerminalGeometry(winsize);
        _runSystemHooks = runSystemHooks;
        _pumpFactory = pumpFactory;
        _exitGuardFactory = exitGuardFactory ?? (restore => new ExitGuard(restore));
        try
        {
            _geometry.Refresh();
        }
        catch
        {
            /* no TTY */
        }
    }

    public int Columns => _geometry.Columns;

    public int Rows => _geometry.Rows;

    /// <summary>True while the tty is in raw mode (04-C2 guard surface).</summary>
    internal bool IsRawMode => _rawMode.IsRaw;

    /// <summary>True when a SIGWINCH hook is installed (04-H5 wiring surface).</summary>
    internal bool ResizeSignalRegistered => _winch is not null;

    /// <summary>True when the abnormal-exit restore hook is armed (04-H2 surface).</summary>
    internal bool ExitGuardInstalled => _exitGuard is { IsRegistered: true };

    /// <summary>The stdin reader of the current session (04-H3/H4 wiring surface);
    /// null when <see cref="Start"/> ran without system hooks.</summary>
    internal StdinPump? Pump => _pump;

    /// <summary>The reader task's fault, if it ended abnormally (04-H3: the
    /// observation surface that replaces the discarded Task).</summary>
    internal Exception? ReaderFault => _readerFault;

    /// <summary>
    /// Why the tty could not be put into raw mode for this session (null when it
    /// could). F10b: <see cref="RawModeSession.Enter"/>'s result used to be discarded,
    /// so a refused switch produced a UI that received line-buffered, echoed keys and
    /// a <c>Stop()</c> that restored a snapshot nobody had taken.
    /// </summary>
    public TerminalFault? ModeFault => _modeFault;

    public void Start(Action<string> onInput, Action onResize)
    {
        _onResize = onResize;
        _started = true;

        // raw mode — Enter() snapshots the original termios so Stop() can write it back.
        // F10b: the result is no longer dropped. A failed switch degrades the session
        // (recorded on ModeFault, announced on stderr) instead of pretending it worked;
        // Start() must not throw here, because the caller has already committed to a TUI.
        _modeFault = null;
        if (!_rawMode.Enter())
        {
            _modeFault = new TerminalFault(
                "raw mode (termios)",
                _rawMode.LastEnterFailure ?? "tcsetattr/tcgetattr refused the raw switch"
            );
            WarnDegraded(_modeFault);
        }

        // 04-H5: geometry used to be read once in the constructor; SIGWINCH now
        // re-queries it and fires the resize callback.
        RegisterResizeSignal();

        // 04-H2: restore raw mode even when the process dies without ExitAsync.
        _exitGuard = _exitGuardFactory(() => _rawMode.Restore());
        _exitGuard.Register();

        if (!_runSystemHooks)
            return;

        StartReader(onInput);
    }

    /// <summary>
    /// F10b: a degraded session must say so. The alternative — a UI that draws but
    /// receives no keystrokes — is indistinguishable from a hung application, and the
    /// user has no way to learn that the tty refused them.
    /// </summary>
    private void WarnDegraded(TerminalFault fault)
    {
        if (!_runSystemHooks)
            return; // test/in-process mode: ModeFault is the observation surface
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
    /// 04-H3 ≡ 06-H2 / 04-H4: stdin is read by <see cref="StdinPump"/> — one stream
    /// per session, EOF ends the loop, one callback per framed input unit. The
    /// previous inline loop re-opened <c>Console.OpenStandardInput()</c> every
    /// iteration, never broke on <c>n == 0</c> (a closed stdin pegged a core) and
    /// handed whole 4096-byte blocks to <c>onInput</c>.
    /// </summary>
    private void StartReader(Action<string> onInput)
    {
        // a restarted session must not keep reading through the previous session's
        // (cancelled, still blocked) reader — dispose it instead of leaking it
        _pump?.Dispose();

        var pump = _pumpFactory(onInput);
        _pump = pump;

        // TuiLifecycle.Enter writes ?2004h immediately after Start(), so the framing
        // buffer must already know a paste body when its first byte arrives.
        pump.Buffer.SetPasteMode(true);

        pump.Start();

        // 04-H3: the reader task is observed, not discarded (the old code threw its
        // Task away). A loop that dies for a reason the pump does not swallow is
        // recorded here instead of surfacing as an UnobservedTaskException later.
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
    /// Re-queries the terminal size and invokes the resize callback when it changed.
    /// Public so a frame loop can drive it (Windows has no SIGWINCH).
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

        _winch?.Dispose();
        _winch = null;

        // F10b (04-C2 residual): Stop() — not Dispose() — is the shutdown every host
        // runs (ExitAsync calls it). Cancelling the token alone left the blocked Read
        // pending on an open stdin, so the orphan pump ate the *next* keystroke of the
        // still-running process and held the tty open until it died. Dispose() closes
        // the stream, which is what unblocks such a read.
        var pump = _pump;
        _pump = null;
        if (_started)
            pump?.Dispose();

        // Idempotent: writes the snapshot captured by Start(), never a fresh tcgetattr.
        // Restore must stay the last word on the tty state (04-C2): the pump never
        // writes to the terminal, and it is cancelled *and* closed above, so a read
        // that was already blocked cannot deliver a byte past this point.
        _rawMode.Restore();
    }

    public void Dispose()
    {
        // Stop() already released the reader (F10b); Dispose() only has to be safe to
        // call without a preceding Stop().
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

    private void RegisterResizeSignal()
    {
        if (OperatingSystem.IsWindows())
            return; // no SIGWINCH; the frame loop calls RefreshGeometry() instead
        try
        {
            _winch = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, OnWindowChanged);
        }
        catch (PlatformNotSupportedException)
        {
            /* degraded mode: no resize notification */
        }
    }

    private void OnWindowChanged(PosixSignalContext context)
    {
        context.Cancel = true; // keep the runtime's default (ignore) from interfering
        RefreshGeometry();
    }
}
