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
/// </summary>
internal sealed class TerminalGeometry
{
    private readonly IWinsizeProbe _probe;
    private int _columns = 80;
    private int _rows = 24;

    internal TerminalGeometry(IWinsizeProbe probe) => _probe = probe;

    internal int Columns => Volatile.Read(ref _columns);

    internal int Rows => Volatile.Read(ref _rows);

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
