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

public readonly record struct Cell(char Ch, Style Style);
