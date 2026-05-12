namespace PicoTUI;

/// <summary>
/// Represents an ANSI/VT terminal color. Supports standard 16 named colors,
/// a 256-color palette index, and full 24-bit (RGB) colors.
/// </summary>
public readonly struct Color : IEquatable<Color>
{
    private readonly ColorKind _kind;
    private readonly byte _index;   // palette index (0-255) for Indexed
    private readonly byte _r, _g, _b; // for Rgb

    private Color(ColorKind kind, byte index = 0, byte r = 0, byte g = 0, byte b = 0)
    {
        _kind = kind;
        _index = index;
        _r = r;
        _g = g;
        _b = b;
    }

    // ── Named colors ──────────────────────────────────────────────────────────
    public static readonly Color Default = new(ColorKind.Default);
    public static readonly Color Black = new(ColorKind.Named, (byte)NamedColor.Black);
    public static readonly Color DarkRed = new(ColorKind.Named, (byte)NamedColor.DarkRed);
    public static readonly Color DarkGreen = new(ColorKind.Named, (byte)NamedColor.DarkGreen);
    public static readonly Color DarkYellow = new(ColorKind.Named, (byte)NamedColor.DarkYellow);
    public static readonly Color DarkBlue = new(ColorKind.Named, (byte)NamedColor.DarkBlue);
    public static readonly Color DarkMagenta = new(ColorKind.Named, (byte)NamedColor.DarkMagenta);
    public static readonly Color DarkCyan = new(ColorKind.Named, (byte)NamedColor.DarkCyan);
    public static readonly Color Gray = new(ColorKind.Named, (byte)NamedColor.Gray);
    public static readonly Color DarkGray = new(ColorKind.Named, (byte)NamedColor.DarkGray);
    public static readonly Color Red = new(ColorKind.Named, (byte)NamedColor.Red);
    public static readonly Color Green = new(ColorKind.Named, (byte)NamedColor.Green);
    public static readonly Color Yellow = new(ColorKind.Named, (byte)NamedColor.Yellow);
    public static readonly Color Blue = new(ColorKind.Named, (byte)NamedColor.Blue);
    public static readonly Color Magenta = new(ColorKind.Named, (byte)NamedColor.Magenta);
    public static readonly Color Cyan = new(ColorKind.Named, (byte)NamedColor.Cyan);
    public static readonly Color White = new(ColorKind.Named, (byte)NamedColor.White);

    // ── Factory methods ───────────────────────────────────────────────────────
    /// <summary>Creates a color from the 256-color palette (0–255).</summary>
    public static Color Indexed(byte index) => new(ColorKind.Indexed, index);

    /// <summary>Creates a 24-bit RGB color.</summary>
    public static Color Rgb(byte r, byte g, byte b) => new(ColorKind.Rgb, 0, r, g, b);

    // ── Properties ────────────────────────────────────────────────────────────
    public ColorKind Kind => _kind;
    public byte Index => _index;
    public byte R => _r;
    public byte G => _g;
    public byte B => _b;

    // ── ANSI helpers ──────────────────────────────────────────────────────────
    /// <summary>
    /// Appends the ANSI foreground color escape sequence code to the provided builder.
    /// Does not include the surrounding ESC[ or 'm' — only the numeric parameters.
    /// </summary>
    internal void AppendForeground(ref AnsiBuilder builder)
    {
        switch (_kind)
        {
            case ColorKind.Default:
                builder.Append("39");
                break;
            case ColorKind.Named when _index < 8:
                builder.Append((30 + _index).ToString());
                break;
            case ColorKind.Named:
                builder.Append((82 + _index).ToString()); // 90-97 for bright (index 8-15)
                break;
            case ColorKind.Indexed:
                builder.Append("38;5;");
                builder.Append(_index.ToString());
                break;
            case ColorKind.Rgb:
                builder.Append("38;2;");
                builder.Append(_r.ToString());
                builder.Append(';');
                builder.Append(_g.ToString());
                builder.Append(';');
                builder.Append(_b.ToString());
                break;
        }
    }

    /// <summary>
    /// Appends the ANSI background color escape sequence code to the provided builder.
    /// </summary>
    internal void AppendBackground(ref AnsiBuilder builder)
    {
        switch (_kind)
        {
            case ColorKind.Default:
                builder.Append("49");
                break;
            case ColorKind.Named when _index < 8:
                builder.Append((40 + _index).ToString());
                break;
            case ColorKind.Named:
                builder.Append((92 + _index).ToString()); // 100-107 for bright (index 8-15)
                break;
            case ColorKind.Indexed:
                builder.Append("48;5;");
                builder.Append(_index.ToString());
                break;
            case ColorKind.Rgb:
                builder.Append("48;2;");
                builder.Append(_r.ToString());
                builder.Append(';');
                builder.Append(_g.ToString());
                builder.Append(';');
                builder.Append(_b.ToString());
                break;
        }
    }

    // ── Equality ──────────────────────────────────────────────────────────────
    public bool Equals(Color other) =>
        _kind == other._kind && _index == other._index &&
        _r == other._r && _g == other._g && _b == other._b;

    public override bool Equals(object? obj) => obj is Color c && Equals(c);

    public override int GetHashCode() => HashCode.Combine(_kind, _index, _r, _g, _b);

    public static bool operator ==(Color left, Color right) => left.Equals(right);
    public static bool operator !=(Color left, Color right) => !left.Equals(right);

    public override string ToString() => _kind switch
    {
        ColorKind.Default => "Default",
        ColorKind.Named => ((NamedColor)_index).ToString(),
        ColorKind.Indexed => $"Indexed({_index})",
        ColorKind.Rgb => $"#{_r:X2}{_g:X2}{_b:X2}",
        _ => "Unknown"
    };
}

/// <summary>Discriminates between the different color representations.</summary>
public enum ColorKind : byte
{
    Default = 0,
    Named = 1,
    Indexed = 2,
    Rgb = 3
}

/// <summary>The 16 standard ANSI named colors (indices 0–15).</summary>
internal enum NamedColor : byte
{
    Black = 0,
    DarkRed = 1,
    DarkGreen = 2,
    DarkYellow = 3,
    DarkBlue = 4,
    DarkMagenta = 5,
    DarkCyan = 6,
    Gray = 7,
    DarkGray = 8,
    Red = 9,
    Green = 10,
    Yellow = 11,
    Blue = 12,
    Magenta = 13,
    Cyan = 14,
    White = 15
}

/// <summary>Text display attributes (bold, italic, etc.).</summary>
[Flags]
public enum TextAttributes : byte
{
    None = 0,
    Bold = 1 << 0,
    Dim = 1 << 1,
    Italic = 1 << 2,
    Underline = 1 << 3,
    Blink = 1 << 4,
    Reverse = 1 << 5,
    Hidden = 1 << 6,
    Strikethrough = 1 << 7
}

/// <summary>Combines foreground color, background color, and text attributes into one value.</summary>
public readonly record struct Style(Color Foreground, Color Background, TextAttributes Attributes)
{
    /// <summary>Default style: terminal default colors, no attributes.</summary>
    public static readonly Style Default = new(Color.Default, Color.Default, TextAttributes.None);

    public Style WithForeground(Color fg) => new(fg, Background, Attributes);
    public Style WithBackground(Color bg) => new(Foreground, bg, Attributes);
    public Style WithAttributes(TextAttributes attrs) => new(Foreground, Background, attrs);
    public Style AddAttributes(TextAttributes attrs) => new(Foreground, Background, Attributes | attrs);
    public Style RemoveAttributes(TextAttributes attrs) => new(Foreground, Background, Attributes & ~attrs);
}
