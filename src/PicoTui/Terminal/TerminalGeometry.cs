namespace PicoTui.Terminal;

/// <summary>
/// Terminal size source. Split out so geometry refresh can be exercised without a
/// real console/tty (04-H5 guard tests inject a fake, then change its answer).
/// </summary>
internal interface IWinsizeProbe
{
    bool TryRead(out int columns, out int rows);
}

/// <summary>
/// Production probe: TIOCGWINSZ on Linux, the console <em>window rectangle</em> on
/// Windows, then <see cref="Console"/> as a last resort.
/// </summary>
internal sealed class TtyWinsizeProbe : IWinsizeProbe
{
    internal static readonly TtyWinsizeProbe Instance = new();

    public bool TryRead(out int columns, out int rows)
    {
        columns = 0;
        rows = 0;

        if (OperatingSystem.IsLinux())
        {
            try
            {
                using var tty = new FileStream("/dev/tty", FileMode.Open, FileAccess.ReadWrite);
                var fd = (int)tty.SafeFileHandle.DangerousGetHandle();
                if (
                    TermiosNative.ioctl(fd, TermiosNative.TIOCGWINSZ, out var ws) == 0
                    && ws.Col > 0
                    && ws.Row > 0
                )
                {
                    columns = ws.Col;
                    rows = ws.Row;
                    return true;
                }
            }
            catch
            {
                /* no tty */
            }
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var handle = ConsoleApiNative.GetStdHandle(ConsoleApiNative.StdOutputHandle);
                if (
                    handle != -1
                    && ConsoleApiNative.GetConsoleScreenBufferInfo(handle, out var info) != 0
                )
                {
                    // 04-H5: the visible window rect, not BufferInfo.Size — Size is the
                    // scrollback buffer (default 9001 rows), never the painted area.
                    (columns, rows) = ConsoleApiNative.VisibleSize(in info);
                    if (columns > 0 && rows > 0)
                        return true;
                }
            }
            catch
            {
                /* fall through to Console fallback */
            }
        }

        try
        {
            columns = Math.Max(1, Console.WindowWidth);
            rows = Math.Max(1, Console.WindowHeight);
            return true;
        }
        catch
        {
            return false; // no console — keep the 80x24 default
        }
    }
}

/// <summary>
/// Cached geometry with an explicit refresh path (04-H5: it used to be queried
/// once in the constructor, so a resized window kept the old dimensions).
/// <para>
/// F13 — the SIGWINCH hook lives here instead of in <see cref="UnixTerminal"/>, for the
/// same reason <see cref="ExitGuard"/> takes an <see cref="IProcessExitSource"/>: an
/// invisible side effect cannot be a guard. The registration goes through the injectable
/// <see cref="SignalRegistrar"/>, the result is recorded
/// (<see cref="RegisteredResizeSignals"/>, <see cref="ResizeSignalRegistered"/>), and
/// <see cref="ResizeSignalRegistered"/> is derived from a live handle — never from intent.
/// A test can therefore inject a registrar that exists on every OS and assert the wiring on
/// Windows too, where the old guard compared two <see langword="false"/> values.
/// </para>
/// <para>
/// The platform gate sits inside <see cref="DefaultSignalRegistrar"/>, NOT inside
/// <see cref="RegisterResizeSignal"/>: an injected registrar is always consulted, which is
/// what makes the seam testable everywhere.
/// </para>
/// </summary>
internal sealed class TerminalGeometry
{
    private readonly IWinsizeProbe _probe;
    private readonly SignalRegistrar _resizeSignalRegistrar;
    private readonly List<PosixSignal> _registeredResizeSignals = [];
    private IDisposable? _resizeSignalHandle;
    private int _columns = 80;
    private int _rows = 24;

    internal TerminalGeometry(IWinsizeProbe probe)
        : this(probe, DefaultSignalRegistrar) { }

    internal TerminalGeometry(IWinsizeProbe probe, SignalRegistrar? resizeSignalRegistrar)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _resizeSignalRegistrar = resizeSignalRegistrar ?? DefaultSignalRegistrar;
    }

    internal int Columns => Volatile.Read(ref _columns);

    internal int Rows => Volatile.Read(ref _rows);

    /// <summary>The resize signals this geometry actually registered, in order. Empty
    /// before <see cref="RegisterResizeSignal"/> and after
    /// <see cref="ReleaseResizeSignal"/>.</summary>
    internal IReadOnlyList<PosixSignal> RegisteredResizeSignals => _registeredResizeSignals;

    /// <summary>True only while a live resize hook exists — derived from the handle the
    /// registrar returned, so a registration that threw or returned nothing reads as
    /// "not armed" instead of "asked for".</summary>
    internal bool ResizeSignalRegistered => _resizeSignalHandle is not null;

    /// <summary>
    /// 04-H5: installs the SIGWINCH hook so a resized window re-queries the size instead of
    /// keeping the startup dimensions. Returns true only when a hook was really installed;
    /// an unavailable signal (the Windows console has none) is reported as
    /// <see langword="false"/>, never counted as armed.
    /// </summary>
    // CA1416: naming SIGWINCH here is not calling a Windows-unsupported API. The value is
    // only a signal identity handed to the registrar; DefaultSignalRegistrar refuses on
    // Windows before any registration happens, and an injected registrar is the test seam —
    // which must be consulted on every OS, otherwise the guard this method exists for is
    // untestable exactly where it was broken.
#pragma warning disable CA1416
    internal bool RegisterResizeSignal(Action<PosixSignalContext> handler)
    {
        if (_resizeSignalHandle is not null)
            return true; // already armed — one hook per geometry

        IDisposable? handle;
        try
        {
            handle = _resizeSignalRegistrar(PosixSignal.SIGWINCH, handler);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NotSupportedException)
        {
            return false; // no SIGWINCH here: the frame loop calls Refresh() instead
        }

        if (handle is null)
            return false; // a registrar that returned nothing registered nothing

        _resizeSignalHandle = handle;
        _registeredResizeSignals.Add(PosixSignal.SIGWINCH);
        return true;
    }
#pragma warning restore CA1416

    /// <summary>Releases the resize hook (symmetrical with
    /// <see cref="RegisterResizeSignal"/>). Safe to call when nothing is armed.</summary>
    internal void ReleaseResizeSignal()
    {
        var handle = _resizeSignalHandle;
        _resizeSignalHandle = null;
        _registeredResizeSignals.Clear();
        try
        {
            handle?.Dispose();
        }
        catch
        {
            /* shutdown must not throw */
        }
    }

    /// <summary>The production registrar: <see cref="PosixSignalRegistration"/> where the
    /// signal exists. The platform gate lives here so that an injected registrar — the test
    /// seam — is always consulted, on every OS.</summary>
    private static IDisposable? DefaultSignalRegistrar(
        PosixSignal signal,
        Action<PosixSignalContext> handler
    )
    {
        if (OperatingSystem.IsWindows())
            // The Windows console has no SIGWINCH at all. Throwing the exception that
            // PosixSignalRegistration would throw keeps the CA1416 flow analysis — and
            // therefore the build — honest.
            throw new PlatformNotSupportedException(
                "The Windows console has no SIGWINCH; resize is polled by the frame loop."
            );
        return PosixSignalRegistration.Create(signal, handler);
    }

    /// <summary>Re-queries the size; returns true when the dimensions changed.</summary>
    internal bool Refresh()
    {
        if (!_probe.TryRead(out var columns, out var rows) || columns <= 0 || rows <= 0)
            return false;

        var changed =
            columns != Volatile.Read(ref _columns) || rows != Volatile.Read(ref _rows);
        Volatile.Write(ref _columns, columns);
        Volatile.Write(ref _rows, rows);
        return changed;
    }
}
