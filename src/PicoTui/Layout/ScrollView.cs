namespace PicoTui.Layout;

public sealed record ScrollOptions(
    bool FollowEnd = false,
    bool Primary = false,
    string Overscroll = "none"
);

public sealed class ScrollView : IComponent
{
    private readonly IComponent _child;
    private readonly ScrollOptions _opts;
    private int _offset;

    public ScrollView(IComponent child, ScrollOptions opts)
    {
        _child = child;
        _opts = opts;
    }

    public void ScrollBy(int n) => _offset = Math.Max(0, _offset + n);

    public void ScrollToEnd(int contentHeight, int viewportHeight) =>
        _offset = Math.Max(0, contentHeight - viewportHeight);

    public string[] RenderViewport(int width, int viewportHeight)
    {
        var content = _child.Render(width);
        if (_opts.FollowEnd)
            _offset = Math.Max(0, content.Length - viewportHeight);

        var lines = new List<string>();
        var end =
            viewportHeight == int.MaxValue
                ? content.Length
                : Math.Min(content.Length, _offset + viewportHeight);
        for (var i = _offset; i < end; i++)
            lines.Add(content[i]);

        // pad only for a real bounded viewport; int.MaxValue means "unbounded" → no padding
        if (viewportHeight != int.MaxValue)
        {
            while (lines.Count < viewportHeight)
                lines.Add("");
        }
        return [.. lines];
    }

    // Unbounded content height: expose the child's full render, no viewport clipping.
    public string[] Render(int width) => _child.Render(width);

    public void HandleInput(string seq) { }

    public void Invalidate() => _child.Invalidate();
}
