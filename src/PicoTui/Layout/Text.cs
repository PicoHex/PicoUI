namespace PicoTui.Layout;

public sealed class Text : IComponent
{
    private string _text = "";

    public Text(string text) => _text = text;

    public void SetText(string text)
    {
        _text = text;
        Invalidate();
    }

    public string[] Render(int width)
    {
        if (width <= 0)
            return [];
        var result = new List<string>();
        foreach (var raw in _text.Split('\n'))
            result.AddRange(WidthTable.WrapToWidth(raw, width));
        return [.. result];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
