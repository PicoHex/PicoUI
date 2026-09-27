namespace PicoTui.Screen;

public readonly record struct RgbColor(byte R, byte G, byte B);

public struct Style : IEquatable<Style>
{
    public bool Bold;
    public bool Italic;
    public bool Underline;
    public bool Reverse;
    public RgbColor? Fg;
    public RgbColor? Bg;

    public readonly bool Equals(Style other) =>
        Bold == other.Bold
        && Italic == other.Italic
        && Underline == other.Underline
        && Reverse == other.Reverse
        && Fg == other.Fg
        && Bg == other.Bg;

    public override readonly bool Equals(object? obj) => obj is Style s && Equals(s);

    public override readonly int GetHashCode() =>
        HashCode.Combine(Bold, Italic, Underline, Reverse, Fg, Bg);

    public static bool operator ==(Style a, Style b) => a.Equals(b);

    public static bool operator !=(Style a, Style b) => !a.Equals(b);
}

/// <summary>
/// One screen cell. <see cref="Rune"/> is a Unicode code point (astral runes
/// included). Wide runes occupy two columns: the rune cell plus a
/// <see cref="Continuation"/> marker cell that encoding skips.
/// </summary>
public readonly record struct Cell(int Rune, Style Style, bool Continuation = false)
{
    /// <summary>Blank cell (space, no attributes).</summary>
    public static readonly Cell Blank = new(' ', default);

    /// <summary>BMP accessor; astral runes report U+FFFD (use <see cref="Rune"/> instead).</summary>
    public char Ch => Rune is >= 0 and <= char.MaxValue ? (char)Rune : '\uFFFD';
}
