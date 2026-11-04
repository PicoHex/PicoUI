namespace PicoTui.Terminal;

/// <summary>
/// termios read/write surface. Split out so the save/restore state machine can be
/// exercised without a real tty (04-C2 guard tests inject a fake).
/// </summary>
internal interface ITermiosOps
{
    bool TryRead(out Termios termios);

    bool TryWrite(in Termios termios);
}

/// <summary>/dev/tty-backed implementation (Linux only; see <see cref="Termios"/>).</summary>
internal sealed class TtyTermiosOps : ITermiosOps
{
    internal static readonly TtyTermiosOps Instance = new();

    public bool TryRead(out Termios termios)
    {
        termios = default;
        if (!OperatingSystem.IsLinux())
            return false; // macOS/BSD termios layout differs — unsupported
        try
        {
            using var tty = new FileStream("/dev/tty", FileMode.Open, FileAccess.ReadWrite);
            var fd = (int)tty.SafeFileHandle.DangerousGetHandle();
            if (TermiosNative.tcgetattr(fd, out var current) != 0)
                return false;
            termios = current;
            return true;
        }
        catch
        {
            return false; // no TTY — degraded mode
        }
    }

    public bool TryWrite(in Termios termios)
    {
        if (!OperatingSystem.IsLinux())
            return false;
        try
        {
            using var tty = new FileStream("/dev/tty", FileMode.Open, FileAccess.ReadWrite);
            var fd = (int)tty.SafeFileHandle.DangerousGetHandle();
            return TermiosNative.tcsetattr(fd, TermiosNative.TCSANOW, in termios) == 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Raw-mode session that owns the pristine termios snapshot (04-C2).
/// <para>
/// The kernel keeps no "pre-raw" snapshot: <c>tcgetattr</c> always reports the
/// <em>current</em> settings, so the old <c>SetRawMode(false)</c> merely wrote the
/// raw values back and was a no-op. <see cref="Enter"/> captures the snapshot,
/// <see cref="Restore"/> writes exactly that snapshot back and never re-reads.
/// </para>
/// <para>Both operations are idempotent and thread-safe.</para>
/// </summary>
internal sealed class RawModeSession
{
    private readonly ITermiosOps _ops;
    private readonly object _gate = new();
    private Termios _saved;
    private bool _raw;

    internal RawModeSession(ITermiosOps ops) => _ops = ops;

    internal bool IsRaw
    {
        get
        {
            lock (_gate)
                return _raw;
        }
    }

    /// <summary>
    /// Why the last <see cref="Enter"/> could not switch the tty to raw mode (null when
    /// it succeeded). F10b: the caller used to drop Enter()'s bool on the floor, so a
    /// tty that refused the switch produced a healthy-looking session in which every
    /// keystroke was echoed and line-buffered.
    /// </summary>
    internal string? LastEnterFailure { get; private set; }

    /// <summary>Captures the original termios and switches to raw. Idempotent.</summary>
    internal bool Enter()
    {
        TermiosNative.AssertLayout();
        lock (_gate)
        {
            if (_raw)
            {
                LastEnterFailure = null;
                return true;
            }
            if (!_ops.TryRead(out var original))
            {
                LastEnterFailure =
                    "tcgetattr returned no snapshot (no /dev/tty, or a non-Linux runtime)";
                return false;
            }

            _saved = original;
            var raw = original;
            raw.c_lflag = TermiosNative.RawLflag(raw.c_lflag);
            raw.c_iflag = TermiosNative.RawIflag(raw.c_iflag);
            raw.c_oflag = TermiosNative.RawOflag(raw.c_oflag);
            raw.c_cflag = TermiosNative.RawCflag(raw.c_cflag);
            if (!_ops.TryWrite(in raw))
            {
                LastEnterFailure = "tcsetattr rejected the raw settings";
                return false; // unchanged tty: do not mark the session as raw
            }
            LastEnterFailure = null;
            _raw = true;
            return true;
        }
    }

    /// <summary>
    /// Writes the saved snapshot back. Idempotent: returns true without touching
    /// the tty when nothing is left to restore. Returns false only when the
    /// tcsetattr call itself failed — a later call then retries.
    /// </summary>
    internal bool Restore()
    {
        lock (_gate)
        {
            if (!_raw)
                return true;
            // A failed tcsetattr keeps _raw set so the next Stop() retries.
            if (!_ops.TryWrite(in _saved))
                return false;
            _raw = false;
            return true;
        }
    }
}
