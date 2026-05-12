namespace PicoTUI;

/// <summary>
/// A single terminal cell: one character and its visual style.
/// </summary>
public readonly record struct Cell(char Character, Style Style)
{
    /// <summary>Empty / blank cell using default terminal style.</summary>
    public static readonly Cell Empty = new(' ', Style.Default);

    /// <summary>Returns <see langword="true"/> if this cell represents a blank space.</summary>
    public bool IsBlank => Character == ' ';
}
