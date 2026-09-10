namespace PicoTui.Layout;

public sealed class StatusBar : IComponent
{
    private string _text = "";
    private string _state = "";

    public void Set(string text)
    {
        _text = text;
        Invalidate();
    }

    public void SetState(string state)
    {
        _state = state;
        Invalidate();
    }

    public string[] Render(int width)
    {
        var state = _state.Length > 0 ? $" [{_state}]" : "";
        var avail = Math.Max(0, width - WidthTable.VisibleWidth(state));
        var text = WidthTable.TruncateToWidth(_text, avail);
        var pad = Math.Max(0, avail - WidthTable.VisibleWidth(text));
        return [$"{text}{new string(' ', pad)}{state}"];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
