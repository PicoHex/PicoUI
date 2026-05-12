using System.Text;

namespace PicoTUI;

/// <summary>
/// A lightweight stack-allocated ANSI escape sequence builder backed by a <see cref="StringBuilder"/>.
/// Used internally to produce ANSI sequences without heap allocations for common operations.
/// </summary>
internal ref struct AnsiBuilder
{
    private readonly StringBuilder _sb;

    public AnsiBuilder(StringBuilder sb)
    {
        _sb = sb;
    }

    public void Append(string value) => _sb.Append(value);
    public void Append(char value) => _sb.Append(value);

    /// <summary>Appends ESC[<paramref name="code"/>m — a single SGR code.</summary>
    public void AppendSgr(string code)
    {
        _sb.Append('\x1B');
        _sb.Append('[');
        _sb.Append(code);
        _sb.Append('m');
    }

    /// <summary>Appends the full SGR sequence for the given <see cref="Style"/>.</summary>
    public void AppendStyle(Style style)
    {
        // Reset + apply all attributes at once
        _sb.Append("\x1B[0");

        var attrs = style.Attributes;
        if ((attrs & TextAttributes.Bold) != 0) { _sb.Append(";1"); }
        if ((attrs & TextAttributes.Dim) != 0) { _sb.Append(";2"); }
        if ((attrs & TextAttributes.Italic) != 0) { _sb.Append(";3"); }
        if ((attrs & TextAttributes.Underline) != 0) { _sb.Append(";4"); }
        if ((attrs & TextAttributes.Blink) != 0) { _sb.Append(";5"); }
        if ((attrs & TextAttributes.Reverse) != 0) { _sb.Append(";7"); }
        if ((attrs & TextAttributes.Hidden) != 0) { _sb.Append(";8"); }
        if ((attrs & TextAttributes.Strikethrough) != 0) { _sb.Append(";9"); }

        // Foreground
        _sb.Append(';');
        style.Foreground.AppendForeground(ref this);

        // Background
        _sb.Append(';');
        style.Background.AppendBackground(ref this);

        _sb.Append('m');
    }

    /// <summary>Moves the cursor to the 1-based (col, row) position.</summary>
    public void AppendMoveTo(int col, int row)
    {
        _sb.Append("\x1B[");
        _sb.Append(row.ToString());
        _sb.Append(';');
        _sb.Append(col.ToString());
        _sb.Append('H');
    }
}
