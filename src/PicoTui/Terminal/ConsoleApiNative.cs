namespace PicoTui.Terminal;

internal static partial class ConsoleApiNative
{
    internal const uint EnableProcessedInput = 0x1;
    internal const uint EnableLineInput = 0x2;
    internal const uint EnableEchoInput = 0x4;
    internal const uint EnableVirtualTerminalProcessing = 0x4;
    internal const uint EnableVirtualTerminalInput = 0x200;
    internal const uint DisableNewlineAutoReturn = 0x8;
    internal const int StdOutputHandle = -11;
    internal const int StdInputHandle = -10;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int SetConsoleMode(nint hConsoleHandle, uint dwMode);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Coord
    {
        public short X,
            Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SmallRect
    {
        public short Left,
            Top,
            Right,
            Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ConsoleScreenBufferInfo
    {
        public Coord Size,
            CursorPosition;
        public short Attributes;
        public SmallRect Window;
        public Coord MaximumWindowSize;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial int GetConsoleScreenBufferInfo(
        nint hConsoleOutput,
        out ConsoleScreenBufferInfo lpConsoleScreenBufferInfo
    );

    /// <summary>Output mode with VT processing + no newline auto-return (pure flag composition).</summary>
    internal static uint WithVtMode(uint mode) =>
        mode | EnableVirtualTerminalProcessing | DisableNewlineAutoReturn;

    /// <summary>
    /// Visible (paintable) console size from the window rectangle. 04-H5: the buffer
    /// <c>Size</c> field is the scrollback extent (9001 rows by default), so using it
    /// made Windows render a 9001-row frame.
    /// </summary>
    internal static (int Columns, int Rows) VisibleSize(in ConsoleScreenBufferInfo info) =>
        (
            info.Window.Right - info.Window.Left + 1,
            info.Window.Bottom - info.Window.Top + 1
        );

    /// <summary>
    /// Input mode that behaves like a raw terminal: VT input sequences enabled and
    /// <c>ENABLE_LINE_INPUT</c>/<c>ENABLE_ECHO_INPUT</c> cleared, otherwise ReadFile
    /// only returns after Enter and the console double-echoes every keystroke (04-C4).
    /// <c>ENABLE_PROCESSED_INPUT</c> is left alone so Ctrl-C still reaches the process.
    /// </summary>
    internal static uint WithVtInputMode(uint mode) =>
        (mode & ~(EnableLineInput | EnableEchoInput)) | EnableVirtualTerminalInput;
}
