namespace PicoTui.Terminal;

public sealed class UnixTerminal : ITerminal
{
    private int _cols = 80;
    private int _rows = 24;
    private bool _raw;
    private readonly CancellationTokenSource _cts = new();

    // audit L9: incremental UTF-8 decoding across stdin chunk boundaries.
    private readonly PicoTui.Input.Utf8ChunkDecoder _decoder = new();

    public UnixTerminal()
    {
        try
        {
            QuerySize();
        }
        catch
        {
            /* no TTY */
        }
    }

    public int Columns => _cols;
    public int Rows => _rows;

    public void Start(Action<string> onInput, Action onResize)
    {
        // raw mode
        _raw = SetRawMode(true);
        // stdin reader thread
        _ = Task.Run(
            async () =>
            {
                var buf = new byte[4096];
                while (!_cts.IsCancellationRequested)
                {
                    var n = Console.OpenStandardInput().Read(buf, 0, buf.Length);
                    if (n > 0)
                        onInput(_decoder.Decode(buf, 0, n));
                }
            },
            _cts.Token
        );
        // SIGWINCH → resize callback (SIGWINCH handling is OS-specific; v1 polls
        // size every frame instead — see Task 9 note. Hook retained for future.)
    }

    public void Stop()
    {
        _cts.Cancel();
        if (_raw)
            SetRawMode(false);
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

    private static bool SetRawMode(bool on)
    {
        // Linux is the tested termios path; macOS needs a different struct layout
        // (recorded follow-up), Windows uses SetConsoleMode (Task 3).
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            return false;
        try
        {
            using var tty = new FileStream("/dev/tty", FileMode.Open, FileAccess.ReadWrite);
            var fd = (int)tty.SafeFileHandle.DangerousGetHandle();
            if (TermiosNative.tcgetattr(fd, out var t) != 0)
                return false;
            if (on)
            {
                t.c_lflag = TermiosNative.RawLflag(t.c_lflag);
                t.c_iflag = TermiosNative.RawIflag(t.c_iflag);
            }
            return TermiosNative.tcsetattr(fd, TermiosNative.TCSANOW, in t) == 0;
        }
        catch
        {
            return false; // no TTY — degraded mode
        }
    }

    private void QuerySize()
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
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
                    _cols = ws.Col;
                    _rows = ws.Row;
                    return;
                }
            }
            catch
            {
                /* no tty */
            }
        }
        try
        {
            _cols = Math.Max(1, Console.WindowWidth);
            _rows = Math.Max(1, Console.WindowHeight);
        }
        catch
        {
            /* 80x24 default */
        }
    }
}
