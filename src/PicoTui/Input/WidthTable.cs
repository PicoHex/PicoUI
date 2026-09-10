namespace PicoTui.Input;

public static class WidthTable
{
    public static int VisibleWidth(string s)
    {
        var w = 0;
        foreach (var r in s.EnumerateRunes())
        {
            var c = r.Value;
            if (IsCombining(c))
                continue;
            w += IsWide(c) ? 2 : 1;
        }
        return w;
    }

    public static int VisibleWidth(char c) => VisibleWidth(c.ToString());

    public static string TruncateToWidth(string s, int max)
    {
        if (max <= 0)
            return "";
        var sb = new StringBuilder();
        var w = 0;
        foreach (var r in s.EnumerateRunes())
        {
            if (IsCombining(r.Value))
                continue;
            var cw = IsWide(r.Value) ? 2 : 1;
            if (w + cw > max)
                break;
            sb.Append(r);
            w += cw;
        }
        return sb.ToString();
    }

    public static bool IsWide(int c) =>
        c
            is >= 0x1100
                and <= 0x115f
                or >= 0x2e80
                and <= 0x303e
                or >= 0x3041
                and <= 0x33ff
                or >= 0x3400
                and <= 0x4dbf
                or >= 0x4e00
                and <= 0x9fff
                or >= 0xa000
                and <= 0xa4cf
                or >= 0xac00
                and <= 0xd7a3
                or >= 0xf900
                and <= 0xfaff
                or >= 0xfe30
                and <= 0xfe4f
                or >= 0xff00
                and <= 0xff60
                or >= 0xffe0
                and <= 0xffe6
                or >= 0x1f300
                and <= 0x1faff
                or >= 0x20000
                and <= 0x3fffd;

    public static bool IsCombining(int c) =>
        c
            is >= 0x0300
                and <= 0x036f
                or >= 0x1ab0
                and <= 0x1aff
                or >= 0x20d0
                and <= 0x20ff
                or >= 0xfe20
                and <= 0xfe2f;
}
