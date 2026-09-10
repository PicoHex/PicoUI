namespace PicoTui.Terminal;

internal static partial class ConsoleApiNative
{
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

    /// <summary>Input mode with VT input sequences enabled (pure flag composition).</summary>
    internal static uint WithVtInputMode(uint mode) => mode | EnableVirtualTerminalInput;
}
