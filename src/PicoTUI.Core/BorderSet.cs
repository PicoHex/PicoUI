namespace PicoTUI;

/// <summary>
/// A set of characters used to draw borders. Several predefined styles are provided.
/// </summary>
public readonly record struct BorderSet(
    char TopLeft,
    char Top,
    char TopRight,
    char Right,
    char BottomRight,
    char Bottom,
    char BottomLeft,
    char Left,
    char VerticalLeft,
    char VerticalRight,
    char HorizontalTop,
    char HorizontalBottom,
    char Cross)
{
    // ── Predefined border styles ──────────────────────────────────────────────

    /// <summary>Single-line thin border (─ │ ┌ ┐ └ ┘).</summary>
    public static readonly BorderSet Single = new(
        TopLeft: '┌', Top: '─', TopRight: '┐',
        Right: '│', BottomRight: '┘', Bottom: '─',
        BottomLeft: '└', Left: '│',
        VerticalLeft: '├', VerticalRight: '┤',
        HorizontalTop: '┬', HorizontalBottom: '┴',
        Cross: '┼');

    /// <summary>Double-line border (═ ║ ╔ ╗ ╚ ╝).</summary>
    public static readonly BorderSet Double = new(
        TopLeft: '╔', Top: '═', TopRight: '╗',
        Right: '║', BottomRight: '╝', Bottom: '═',
        BottomLeft: '╚', Left: '║',
        VerticalLeft: '╠', VerticalRight: '╣',
        HorizontalTop: '╦', HorizontalBottom: '╩',
        Cross: '╬');

    /// <summary>Rounded corners with single lines (╭ ─ ╮ │ ╯ ─ ╰ │).</summary>
    public static readonly BorderSet Rounded = new(
        TopLeft: '╭', Top: '─', TopRight: '╮',
        Right: '│', BottomRight: '╯', Bottom: '─',
        BottomLeft: '╰', Left: '│',
        VerticalLeft: '├', VerticalRight: '┤',
        HorizontalTop: '┬', HorizontalBottom: '┴',
        Cross: '┼');

    /// <summary>Thick (heavy) border (━ ┃ ┏ ┓ ┗ ┛).</summary>
    public static readonly BorderSet Thick = new(
        TopLeft: '┏', Top: '━', TopRight: '┓',
        Right: '┃', BottomRight: '┛', Bottom: '━',
        BottomLeft: '┗', Left: '┃',
        VerticalLeft: '┣', VerticalRight: '┫',
        HorizontalTop: '┳', HorizontalBottom: '┻',
        Cross: '╋');

    /// <summary>ASCII-compatible border (+ - |).</summary>
    public static readonly BorderSet Ascii = new(
        TopLeft: '+', Top: '-', TopRight: '+',
        Right: '|', BottomRight: '+', Bottom: '-',
        BottomLeft: '+', Left: '|',
        VerticalLeft: '+', VerticalRight: '+',
        HorizontalTop: '+', HorizontalBottom: '+',
        Cross: '+');
}
