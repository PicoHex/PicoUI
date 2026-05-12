using System.Runtime.InteropServices;
using System.Text;

namespace PicoTUI;

/// <summary>
/// Low-level terminal control: alternate screen, raw mode, cursor, and ANSI output.
/// </summary>
public static class Terminal
{
    // ── Output stream ─────────────────────────────────────────────────────────

    private static readonly StreamWriter _out = new(
        Console.OpenStandardOutput(),
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        bufferSize: 65536,
        leaveOpen: true)
    {
        AutoFlush = false
    };

    // ── Size ──────────────────────────────────────────────────────────────────

    /// <summary>Returns the current terminal window size.</summary>
    public static Size GetSize() => new(Console.WindowWidth, Console.WindowHeight);

    // ── Alternate screen ──────────────────────────────────────────────────────

    /// <summary>Switches to the alternate screen buffer (saves the current screen content).</summary>
    public static void EnterAlternateScreen()
    {
        _out.Write("\x1B[?1049h");
        _out.Flush();
    }

    /// <summary>Restores the normal screen buffer.</summary>
    public static void LeaveAlternateScreen()
    {
        _out.Write("\x1B[?1049l");
        _out.Flush();
    }

    // ── Cursor ────────────────────────────────────────────────────────────────

    /// <summary>Hides the blinking cursor.</summary>
    public static void HideCursor()
    {
        _out.Write("\x1B[?25l");
        _out.Flush();
    }

    /// <summary>Shows the cursor.</summary>
    public static void ShowCursor()
    {
        _out.Write("\x1B[?25h");
        _out.Flush();
    }

    /// <summary>Moves the cursor to the given 0-based (col, row) position.</summary>
    public static void MoveTo(int col, int row)
    {
        // ANSI cursor position is 1-based
        _out.Write($"\x1B[{row + 1};{col + 1}H");
        _out.Flush();
    }

    // ── Screen ────────────────────────────────────────────────────────────────

    /// <summary>Clears the entire screen and moves the cursor to (0,0).</summary>
    public static void Clear()
    {
        _out.Write("\x1B[2J\x1B[H");
        _out.Flush();
    }

    /// <summary>Resets all terminal attributes to their defaults.</summary>
    public static void ResetAttributes()
    {
        _out.Write("\x1B[0m");
        _out.Flush();
    }

    // ── Raw mode ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Enables raw mode so keystrokes are delivered immediately without
    /// line-buffering or echo.
    /// </summary>
    public static void EnableRawMode()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            WindowsRaw.Enable();
        else
            UnixRaw.Enable();
    }

    /// <summary>Disables raw mode, restoring the previous terminal settings.</summary>
    public static void DisableRawMode()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            WindowsRaw.Disable();
        else
            UnixRaw.Disable();
    }

    // ── Buffered write helpers (used by Renderer) ─────────────────────────────

    internal static StreamWriter Out => _out;

    internal static void Flush() => _out.Flush();

    // ── Platform-specific raw mode implementations ────────────────────────────

    private static class UnixRaw
    {
        private static Termios _saved;
        private static bool _hasSaved;

        public static void Enable()
        {
            if (tcgetattr(0, out _saved) != 0) return;
            _hasSaved = true;

            var raw = _saved;

            // Disable canonical mode, echo, signals
            raw.c_lflag &= ~(ICANON | ECHO | ISIG | IEXTEN);
            // Disable flow control, CR translation
            raw.c_iflag &= ~(IXON | ICRNL | BRKINT | INPCK | ISTRIP);
            // No output post-processing
            raw.c_oflag &= ~OPOST;
            // 8-bit characters
            raw.c_cflag |= CS8;

            // Read returns after 1 byte with no timeout
            raw.c_cc[VMIN] = 1;
            raw.c_cc[VTIME] = 0;

            tcsetattr(0, TCSAFLUSH, ref raw);
        }

        public static void Disable()
        {
            if (_hasSaved)
                tcsetattr(0, TCSAFLUSH, ref _saved);
        }

        // ── P/Invoke ─────────────────────────────────────────────────────────

        private const uint ICANON = 0x0200;
        private const uint ECHO = 0x0008;
        private const uint ISIG = 0x0080;
        private const uint IEXTEN = 0x8000;
        private const uint IXON = 0x0200;
        private const uint ICRNL = 0x0100;
        private const uint BRKINT = 0x0002;
        private const uint INPCK = 0x0010;
        private const uint ISTRIP = 0x0020;
        private const uint OPOST = 0x0001;
        private const uint CS8 = 0x0300;

        private const int VMIN = 6;
        private const int VTIME = 5;
        private const int TCSAFLUSH = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct Termios
        {
            public uint c_iflag;
            public uint c_oflag;
            public uint c_cflag;
            public uint c_lflag;
            public byte c_line;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] c_cc;
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int tcgetattr(int fd, out Termios termios);

        [DllImport("libc", SetLastError = true)]
        private static extern int tcsetattr(int fd, int optionalActions, ref Termios termios);
    }

    private static class WindowsRaw
    {
        private static uint _savedMode;
        private static bool _hasSaved;

        private const int STD_INPUT_HANDLE = -10;
        private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
        private const uint ENABLE_PROCESSED_INPUT = 0x0001;
        private const uint ENABLE_LINE_INPUT = 0x0002;
        private const uint ENABLE_ECHO_INPUT = 0x0004;

        public static void Enable()
        {
            var handle = GetStdHandle(STD_INPUT_HANDLE);
            if (!GetConsoleMode(handle, out _savedMode)) return;
            _hasSaved = true;

            uint newMode = (_savedMode & ~(ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT))
                           | ENABLE_VIRTUAL_TERMINAL_INPUT;
            SetConsoleMode(handle, newMode);
        }

        public static void Disable()
        {
            if (_hasSaved)
                SetConsoleMode(GetStdHandle(STD_INPUT_HANDLE), _savedMode);
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
