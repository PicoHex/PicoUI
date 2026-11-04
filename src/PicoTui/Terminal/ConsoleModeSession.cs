namespace PicoTui.Terminal;

/// <summary>
/// Console mode read/write surface (Windows). Split out so save/restore can be
/// exercised without a real console (04-C4 guard tests inject a fake).
/// </summary>
internal interface IConsoleModeOps
{
    bool TryGet(int stdHandle, out uint mode);

    bool TrySet(int stdHandle, uint mode);
}

/// <summary>kernel32-backed implementation.</summary>
internal sealed class Win32ConsoleModeOps : IConsoleModeOps
{
    internal static readonly Win32ConsoleModeOps Instance = new();

    public bool TryGet(int stdHandle, out uint mode)
    {
        mode = 0;
        try
        {
            var handle = ConsoleApiNative.GetStdHandle(stdHandle);
            return handle != -1 && ConsoleApiNative.GetConsoleMode(handle, out mode) != 0;
        }
        catch
        {
            return false; // no console — degraded mode
        }
    }

    public bool TrySet(int stdHandle, uint mode)
    {
        try
        {
            var handle = ConsoleApiNative.GetStdHandle(stdHandle);
            return handle != -1 && ConsoleApiNative.SetConsoleMode(handle, mode) != 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Saves the console in/out modes, switches to VT + raw input, and puts the saved
/// modes back on <see cref="Restore"/> (04-C4: only the input flag was OR-ed in,
/// the originals were never stored and a failing SetConsoleMode went unnoticed).
/// </summary>
internal sealed class ConsoleModeSession
{
    private readonly IConsoleModeOps _ops;
    private readonly object _gate = new();
    private uint _savedOut;
    private uint _savedIn;
    private bool _outSaved;
    private bool _inSaved;
    private int _setFailures;

    internal ConsoleModeSession(IConsoleModeOps ops) => _ops = ops;

    /// <summary>Number of SetConsoleMode calls that failed since construction.</summary>
    internal int SetFailures
    {
        get
        {
            lock (_gate)
                return _setFailures;
        }
    }

    internal bool IsActive
    {
        get
        {
            lock (_gate)
                return _outSaved || _inSaved;
        }
    }

    /// <summary>
    /// Why the last <see cref="Enter"/> could not apply the VT/raw modes (null when it
    /// did). F10b: the caller used to drop Enter()'s result, so a console that rejected
    /// <c>SetConsoleMode</c> produced a session in which the keys never became VT input.
    /// </summary>
    internal string? LastEnterFailure { get; private set; }

    /// <summary>Captures both modes and applies the VT/raw composition. Idempotent.</summary>
    internal bool Enter()
    {
        lock (_gate)
        {
            if (_outSaved || _inSaved)
            {
                LastEnterFailure =
                    _setFailures == 0 ? null : $"SetConsoleMode failed {_setFailures} time(s)";
                return _setFailures == 0;
            }

            var ok = true;
            var failures = new List<string>();

            if (_ops.TryGet(ConsoleApiNative.StdOutputHandle, out var outMode))
            {
                _savedOut = outMode;
                _outSaved = true;
                if (!_ops.TrySet(ConsoleApiNative.StdOutputHandle, ConsoleApiNative.WithVtMode(outMode)))
                {
                    _setFailures++;
                    ok = false;
                    failures.Add("SetConsoleMode(stdout, VT) rejected");
                }
            }
            else
            {
                ok = false;
                failures.Add("GetConsoleMode(stdout) unavailable (no console)");
            }

            if (_ops.TryGet(ConsoleApiNative.StdInputHandle, out var inMode))
            {
                _savedIn = inMode;
                _inSaved = true;
                if (
                    !_ops.TrySet(
                        ConsoleApiNative.StdInputHandle,
                        ConsoleApiNative.WithVtInputMode(inMode)
                    )
                )
                {
                    _setFailures++;
                    ok = false;
                    failures.Add("SetConsoleMode(stdin, VT/raw) rejected");
                }
            }
            else
            {
                ok = false;
                failures.Add("GetConsoleMode(stdin) unavailable (no console)");
            }

            LastEnterFailure = failures.Count == 0 ? null : string.Join("; ", failures);
            return ok;
        }
    }

    /// <summary>Writes the saved in/out modes back. Idempotent.</summary>
    internal bool Restore()
    {
        lock (_gate)
        {
            var ok = true;

            if (_outSaved)
            {
                if (_ops.TrySet(ConsoleApiNative.StdOutputHandle, _savedOut))
                    _outSaved = false;
                else
                    ok = false;
            }

            if (_inSaved)
            {
                if (_ops.TrySet(ConsoleApiNative.StdInputHandle, _savedIn))
                    _inSaved = false;
                else
                    ok = false;
            }

            return ok;
        }
    }
}
