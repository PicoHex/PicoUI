namespace PicoMermaid;

/// <summary>
/// Terminal column math and text hygiene for the renderer.
/// <para>
/// The width model is the one the terminal front-end actually rasterizes with:
/// <c>PicoTui.Input.WidthTable</c> (<c>src/PicoTui/Input/WidthTable.cs</c>, used
/// by <c>PicoTui/Screen/ScreenBuffer.cs</c>), i.e. iterate by rune, count zero
/// for combining marks / format controls, two for East-Asian wide+fullwidth
/// ranges and emoji, one otherwise. It is duplicated here on purpose:
/// <c>PicoTui</c> already references <c>PicoMermaid</c>, so reusing that type
/// would be a circular dependency, and <c>PicoMermaid</c> is published as a
/// zero-dependency package.
/// </para>
/// </summary>
internal static class DisplayWidth
{
    /// <summary>Upper bound for a piece of caller input echoed in a warning.</summary>
    internal const int EchoLimit = 120;

    /// <summary>Display columns consumed by <paramref name="s"/>.</summary>
    public static int Of(string s)
    {
        var width = 0;
        foreach (var rune in s.EnumerateRunes())
            width += Columns(rune.Value);
        return width;
    }

    /// <summary>
    /// Hard truncation to at most <paramref name="max"/> display columns; never
    /// splits a rune (so never splits a UTF-16 surrogate pair or a grapheme's
    /// base char from its combining marks). Zero-width runes are kept: they cost
    /// no columns.
    /// </summary>
    public static string Truncate(string s, int max)
    {
        if (max <= 0)
            return "";
        if (Of(s) <= max)
            return s;
        var sb = new StringBuilder(s.Length);
        var width = 0;
        foreach (var rune in s.EnumerateRunes())
        {
            var columns = Columns(rune.Value);
            if (width + columns > max)
                break;
            sb.Append(rune);
            width += columns;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Drops C0/C1 control characters (0x00-0x1F, 0x7F-0x9F) so that no ANSI
    /// escape, bell, backspace or bare CR reaches a consumer that writes rows
    /// straight to a terminal. <c>\n</c> and <c>\t</c> are structural and kept.
    /// </summary>
    public static string Clean(string s)
    {
        var dirty = false;
        foreach (var c in s)
        {
            if (IsDropped(c))
            {
                dirty = true;
                break;
            }
        }
        if (!dirty)
            return s;

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (!IsDropped(c))
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Single-line, control-free, display-width-bounded echo of caller input.</summary>
    public static string Clip(string s)
    {
        var flat = Clean(s).Replace('\n', ' ').Replace('\t', ' ');
        if (Of(flat) <= EchoLimit)
            return flat;
        return Truncate(flat, EchoLimit - 1) + "…";
    }

    private static bool IsDropped(char c) => c is not '\n' and not '\t' && char.IsControl(c);

    /// <summary>Columns for one rune: zero for format controls/marks, two for wide runes.</summary>
    private static int Columns(int rune) => IsZeroWidth(rune) ? 0 : IsWide(rune) ? 2 : 1;

    internal static bool IsWide(int c) =>
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

    internal static bool IsCombining(int c) =>
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
    internal static bool IsZeroWidth(int c) =>
        IsCombining(c)
        || c is 0x200B or 0x200C or 0x200D or 0x200E or 0x200F or 0x2060 or 0xFEFF
        || c is >= 0xFE00 and <= 0xFE0F
        || c is >= 0xE0100 and <= 0xE01EF;
}
