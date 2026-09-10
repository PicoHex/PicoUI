namespace PicoTui.Layout;

public sealed class HStack : IComponent
{
    private readonly List<LayoutItem> _items = [];

    public void Add(LayoutItem item) => _items.Add(item);

    public string[] Render(int width)
    {
        var visible = _items.Where(i => i.Visible is null || i.Visible(width, 0)).ToList();

        var result = new List<string> { "" };
        var used = 0;
        foreach (var item in visible)
        {
            var lines = item.Component.Render(width);
            var text = lines.Length > 0 ? lines[0] : "";
            var avail = Math.Max(0, width - used);
            text = WidthTable.TruncateToWidth(text, avail);
            result[0] += text;
            used += WidthTable.VisibleWidth(text);
            if (used >= width)
                break;
        }
        return [.. result];
    }

    public void HandleInput(string seq) { }

    public void Invalidate()
    {
        foreach (var item in _items)
            item.Component.Invalidate();
    }
}
