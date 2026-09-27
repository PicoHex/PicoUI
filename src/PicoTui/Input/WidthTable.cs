namespace PicoTui.Input;

public static class WidthTable
{
    public static int VisibleWidth(string s)
    {
        var w = 0;
        foreach (var r in s.EnumerateRunes())
        {
            if (IsZeroWidth(r.Value))
                continue;
            w += IsWide(r.Value) ? 2 : 1;
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
            if (IsZeroWidth(r.Value))
                continue;
            var cw = RuneColumns(r.Value);
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

    /// <summary>
    /// Zero-width runes: combining marks plus the format controls that must not
    /// consume a cell — joiners, zero-width spaces, LRM/RLM, word joiner, BOM and
    /// the variation selectors (including the supplementary plane).
    /// </summary>
    public static bool IsZeroWidth(int c) =>
        IsCombining(c)
        || c is 0x200B or 0x200C or 0x200D or 0x200E or 0x200F or 0x2060 or 0xFEFF
        || c is >= 0xFE00 and <= 0xFE0F
        || c is >= 0xE0100 and <= 0xE01EF;

    /// <summary>Hard-wraps to display width without splitting a rune.
    /// Non-positive width yields no lines (callers must not spin).</summary>
    public static string[] WrapToWidth(string s, int max)
    {
        if (max <= 0)
            return [];
        if (s.Length == 0)
            return [""];
        var lines = new List<string>();
        var sb = new StringBuilder();
        var w = 0;
        foreach (var r in s.EnumerateRunes())
        {
            if (IsZeroWidth(r.Value))
            {
                sb.Append(r); // kept in the text: joiners/selectors belong to it
                continue;
            }
            var cw = RuneColumns(r.Value);
            if (w + cw > max && sb.Length > 0)
            {
                lines.Add(sb.ToString());
                sb.Clear();
                w = 0;
            }
            sb.Append(r);
            w += cw;
        }
        lines.Add(sb.ToString());
        return [.. lines];
    }

    /// <summary>
    /// Replaces the display columns <paramref name="startCol"/>..
    /// <paramref name="startCol"/>+<paramref name="insertWidth"/>-1 of
    /// <paramref name="line"/> with <paramref name="insert"/>. Runes straddling
    /// either boundary are dropped (a wide rune cannot be half replaced) and the
    /// result keeps the line's original column count.
    /// </summary>
    public static string SpliceAtColumns(string line, int startCol, string insert, int insertWidth)
    {
        if (startCol < 0)
            startCol = 0;
        if (insertWidth < 0)
            insertWidth = 0;
        var originalCols = VisibleWidth(line);
        var runes = line.EnumerateRunes().ToArray();
        var sb = new StringBuilder();
        var sourceCol = 0; // columns consumed from the source line
        var emittedCol = 0; // columns written so far
        var idx = 0;

        // complete runes ending at or before the splice point
        while (idx < runes.Length)
        {
            var w = RuneColumns(runes[idx].Value);
            if (sourceCol + w > startCol)
                break;
            sb.Append(runes[idx]);
            idx++;
            sourceCol += w;
            emittedCol += w;
        }
        // the line may be shorter than startCol (or a dropped rune started earlier)
        while (emittedCol < startCol)
        {
            sb.Append(' ');
            emittedCol++;
        }

        // drop every source rune overlapping the inserted region
        var resumeCol = startCol + insertWidth;
        while (idx < runes.Length && sourceCol < resumeCol)
        {
            sourceCol += RuneColumns(runes[idx].Value);
            idx++;
        }

        sb.Append(insert);
        emittedCol += insertWidth;
        if (sourceCol > resumeCol)
        {
            // a dropped wide rune extended past the insert: keep those columns blank
            var blank = sourceCol - resumeCol;
            sb.Append(' ', blank);
            emittedCol += blank;
        }

        while (idx < runes.Length)
            sb.Append(runes[idx++]);

        var result = sb.ToString();
        var resultCols = VisibleWidth(result);
        if (resultCols < originalCols)
            result += new string(' ', originalCols - resultCols);
        return result;
    }

    /// <summary>Columns for one rune: zero for format controls/marks, two for wide runes.</summary>
    private static int RuneColumns(int rune) =>
        IsZeroWidth(rune) ? 0
        : IsWide(rune) ? 2
        : 1;
}
