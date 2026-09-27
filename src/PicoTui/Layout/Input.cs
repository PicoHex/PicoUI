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
            _buffer.Length--;
        else if (seq.Length == 1)
            _buffer.Append(seq[0]);
    }

    public string[] Render(int width)
    {
        var text = _buffer.ToString();
        var start = Math.Max(0, text.Length - width);
        var visible = text.Length > width ? text[start..] : text;
        return [visible];
    }

    public void Invalidate() { }
}
