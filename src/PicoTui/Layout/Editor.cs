namespace PicoTui.Layout;

public sealed class Editor : IComponent
{
    private readonly StringBuilder _buffer = new();
    private bool _focused = true;
    public string Text => _buffer.ToString();
    public int CursorCol { get; private set; }

    public void SetText(string text)
    {
        _buffer.Clear();
        _buffer.Append(text);
        Invalidate();
    }

    public void SetFocus(bool focused) => _focused = focused;

    public void HandleInput(string seq)
    {
        if (!_focused)
            return;
        if (seq == "\r")
            _buffer.Append('\n');
        else if (seq == "\b" || seq == "\x7f")
        {
            if (_buffer.Length > 0)
                _buffer.Length--;
        }
        else if (seq.Length == 1)
            _buffer.Append(seq[0]);
    }

    public string[] Render(int width)
    {
        var result = new List<string>();
        foreach (var raw in Text.Split('\n'))
        {
            if (raw.Length <= width)
                result.Add(raw);
            else
                result.Add(raw[..width]);
        }
        return [.. result];
    }

    public void Invalidate() { }
}
