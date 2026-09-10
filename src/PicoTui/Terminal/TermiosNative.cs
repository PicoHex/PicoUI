namespace PicoTui.Terminal;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Termios
{
    public uint c_iflag,
        c_oflag,
        c_cflag,
        c_lflag;
    public byte c_line;

    // cc_t c_cc[NCCS] — N is platform-specific; fixed buffer is blittable
    // (LibraryImport source-gen rejects MarshalAs(ByValArray) managed fields)
    public fixed byte c_cc[32];
}

[StructLayout(LayoutKind.Sequential)]
internal struct WinSize
{
    public ushort Row,
        Col,
        Xpixel,
        Ypixel;
}

internal static partial class TermiosNative
{
    // Linux libc; macOS uses a different path selected by OperatingSystem.IsMacOS()
    private const string Libc = "libc";

    [LibraryImport(Libc, SetLastError = true)]
    internal static partial int tcgetattr(int fd, out Termios termios);

    [LibraryImport(Libc, SetLastError = true)]
    internal static partial int tcsetattr(int fd, int optionalActions, in Termios termios);

    [LibraryImport(Libc, SetLastError = true)]
    internal static partial int ioctl(int fd, ulong request, out WinSize ws);

    internal const int TCSANOW = 0;
    internal const ulong TIOCGWINSZ = 0x5413; // Linux

    internal static uint RawLflag(uint lflag)
    {
        const uint icanon = 0x2,
            echo = 0x8,
            isig = 0x1;
        return lflag & ~(icanon | echo | isig);
    }

    internal static uint RawIflag(uint iflag)
    {
        const uint ixon = 0x400,
            icanon = 0x2;
        return iflag & ~(ixon | icanon);
    }
}
