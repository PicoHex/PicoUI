namespace PicoTui.Terminal;

// Linux `struct termios` as laid out by asm-generic/termbits.h — glibc and musl
// share this ABI and both declare sizeof == 60 because the two speed_t fields
// follow c_cc[NCCS=32] (04-H1: without them a 52-byte managed buffer received a
// 60-byte tcgetattr write, i.e. an 8-byte out-of-bounds write).
//
// macOS/BSD are NOT supported: Darwin's termios uses byte c_cc[20] and tcflag_t
// is 8 bytes wide, so this layout must never be marshalled there. The only
// termios path is UnixTerminal, which disables raw mode on macOS.
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

    // speed_t c_ispeed, c_ospeed — required for ABI parity (see 04-H1).
    public uint c_ispeed,
        c_ospeed;
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

    /// <summary>sizeof(struct termios) on Linux — 60 for glibc and musl alike.</summary>
    internal const int LinuxTermiosSize = 60;

    [LibraryImport(Libc, SetLastError = true)]
    internal static partial int tcgetattr(int fd, out Termios termios);

    [LibraryImport(Libc, SetLastError = true)]
    internal static partial int tcsetattr(int fd, int optionalActions, in Termios termios);

    [LibraryImport(Libc, SetLastError = true)]
    internal static partial int ioctl(int fd, ulong request, out WinSize ws);

    internal const int TCSANOW = 0;
    internal const ulong TIOCGWINSZ = 0x5413; // Linux

    /// <summary>
    /// Debug-only layout assertion (compiled out in Release: no runtime cost).
    /// Guards 04-H1 — the managed struct must match the native sizeof.
    /// </summary>
    [Conditional("DEBUG")]
    internal static void AssertLayout() =>
        Debug.Assert(
            Unsafe.SizeOf<Termios>() == LinuxTermiosSize,
            "Termios must match Linux struct termios (60 bytes); tcgetattr would write out of bounds"
        );

    /// <summary>Layout check usable from tests in both configurations.</summary>
    internal static bool LayoutIsValid => Unsafe.SizeOf<Termios>() == LinuxTermiosSize;

    internal static uint RawLflag(uint lflag)
    {
        const uint isig = 0x1,
            icanon = 0x2,
            echo = 0x8,
            iexten = 0x8000;
        return lflag & ~(isig | icanon | echo | iexten);
    }

    /// <summary>
    /// Clears the c_iflag family cfmakeraw(3) clears. 0x2 in c_iflag is BRKINT,
    /// not ICANON (which only exists in c_lflag); 0x100 is ICRNL — leaving ICRNL
    /// set is what kept turning CR (0x0D) into LF (0x0A) so Enter never arrived
    /// (04-C3).
    /// </summary>
    internal static uint RawIflag(uint iflag)
    {
        const uint ignbrk = 0x1,
            brkint = 0x2,
            parmrk = 0x8,
            istrip = 0x20,
            inlcr = 0x40,
            igncr = 0x80,
            icrnl = 0x100,
            ixon = 0x400,
            ixoff = 0x1000;
        return iflag
            & ~(ignbrk | brkint | parmrk | istrip | inlcr | igncr | icrnl | ixon | ixoff);
    }

    /// <summary>Clears c_oflag's OPOST (04-C3: output post-processing must be off in raw mode).</summary>
    internal static uint RawOflag(uint oflag)
    {
        const uint opost = 0x1;
        return oflag & ~opost;
    }

    /// <summary>CS8, no parity — matches cfmakeraw(3).</summary>
    internal static uint RawCflag(uint cflag)
    {
        const uint csize = 0x30,
            cs8 = 0x30,
            parenb = 0x100;
        return (cflag & ~(csize | parenb)) | cs8;
    }
}
