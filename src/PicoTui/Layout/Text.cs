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
        var result = new List<string>();
        foreach (var raw in _text.Split('\n'))
        {
            if (raw.Length == 0)
            {
                result.Add("");
                continue;
            }
            if (raw.Length <= width)
            {
                result.Add(raw);
                continue;
            }
            var start = 0;
            while (start < raw.Length)
            {
                var take = Math.Min(width, raw.Length - start);
                result.Add(raw.Substring(start, take));
                start += take;
            }
        }
        return [.. result];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
