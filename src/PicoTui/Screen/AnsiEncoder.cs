namespace PicoTui.Screen;

public static class AnsiEncoder
{
    public const string SgrReset = "\x1b[0m";

    public static string EncodeLine(Line line, bool clearToEnd)
    {
        var sb = new StringBuilder();
        Style? active = null;
        foreach (var cell in line.Cells)
        {
            if (cell.Style != active)
            {
                sb.Append(SgrReset);
                sb.Append(SgrFor(cell.Style));
                active = cell.Style;
            }
            sb.Append(cell.Ch);
        }
        if (active is not null)
            sb.Append(SgrReset);
        if (clearToEnd)
            sb.Append("\x1b[K");
        return sb.ToString();
    }

    public static string WrapSynchronized(string body) => $"\x1b[?2026h{body}\x1b[?2026l";

    private static string SgrFor(Style s)
    {
        var codes = new List<string>();
        if (s.Bold)
            codes.Add("1");
        if (s.Italic)
            codes.Add("3");
        if (s.Underline)
            codes.Add("4");
        if (s.Reverse)
            codes.Add("7");
        if (s.Fg is { } fg)
            codes.Add($"38;2;{fg.R};{fg.G};{fg.B}");
        if (s.Bg is { } bg)
            codes.Add($"48;2;{bg.R};{bg.G};{bg.B}");
        return codes.Count == 0 ? "" : $"\x1b[{string.Join(';', codes)}m";
    }
}
