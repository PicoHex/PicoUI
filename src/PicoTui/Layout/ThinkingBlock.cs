namespace PicoTui.Layout;

public sealed class ThinkingBlock : IComponent
{
    private readonly StringBuilder _body = new();
    public bool Collapsed { get; private set; } = true;

    public void Append(string delta)
    {
        _body.Append(delta);
        Invalidate();
    }

    public void Toggle() => Collapsed = !Collapsed;

    public string[] Render(int width)
    {
        var bodyLines = _body.ToString().Split('\n');
        if (Collapsed)
            return [$"▸ thinking ({bodyLines.Length} lines)"];
        var result = new List<string> { "▸ thinking" };
        result.AddRange(bodyLines.Select(l => l.Length > width ? l[..width] : l));
        return [.. result];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
