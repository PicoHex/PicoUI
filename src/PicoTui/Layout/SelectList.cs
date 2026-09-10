namespace PicoTui.Layout;

public sealed class SelectList : IComponent
{
    private readonly List<string> _items = [];
    public int Selected { get; private set; }

    public void AddItem(string item) => _items.Add(item);

    public void MoveSelection(int delta) =>
        Selected = Math.Clamp(Selected + delta, 0, Math.Max(0, _items.Count - 1));

    public string[] Render(int width)
    {
        var result = new List<string>();
        for (var i = 0; i < _items.Count; i++)
            result.Add(i == Selected ? $"▸ {_items[i]}" : $"  {_items[i]}");
        return [.. result];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
