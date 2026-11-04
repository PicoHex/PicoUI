namespace PicoTui.Layout;

public sealed class Input : IComponent
{
    private readonly StringBuilder _buffer = new();
    public string Text => _buffer.ToString();

    /// <summary>Insertion point: the editor only appends/deletes at the end.</summary>
    public int Cursor => _buffer.Length;

    public void HandleInput(string seq)
    {
        if (seq == "\b" && _buffer.Length > 0)
        {
            // 06-H6: drop a whole rune — removing one UTF-16 code unit leaves an
            // orphan high surrogate behind when the buffer ends with an emoji.
            var last = _buffer.Length - 1;
            var cut = char.IsLowSurrogate(_buffer[last]) && last > 0 && char.IsHighSurrogate(_buffer[last - 1])
                ? 2
                : 1;
            _buffer.Length -= cut;
        }
        else if (seq.Length == 1)
            _buffer.Append(seq[0]);
    }

    public string[] Render(int width)
    {
        // 06-H6: clip by display columns at rune boundaries. Slicing by UTF-16 code
        // units emitted orphan surrogates and could exceed the terminal width (a CJK
        // rune is one code unit but two columns); the tail is shown so the caret at
        // the end of the buffer stays visible.
        var text = WidthTable.TailToWidth(_buffer.ToString(), width);
        return [WidthTable.TruncateToWidth(text, width)];
    }

    public void Invalidate() { }
}
