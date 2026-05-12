namespace PicoTUI.Widgets;

/// <summary>
/// A scrollable list of selectable items.
/// </summary>
public sealed class List : Widget
{
    private readonly List<ListItem> _items = [];
    private int _selectedIndex;
    private int _scrollOffset;

    /// <summary>Style for unselected items.</summary>
    public Style ItemStyle { get; set; } = Style.Default;

    /// <summary>Style for the currently selected item.</summary>
    public Style SelectedStyle { get; set; } = new Style(Color.Black, Color.Cyan, TextAttributes.Bold);

    /// <summary>Style for the highlight bar background.</summary>
    public Style HighlightStyle { get; set; } = new Style(Color.White, Color.DarkBlue, TextAttributes.None);

    /// <summary>The index of the currently selected item, or -1 if the list is empty.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => _selectedIndex = _items.Count == 0 ? -1 : Math.Clamp(value, 0, _items.Count - 1);
    }

    /// <summary>Returns the currently selected item, or <see langword="null"/>.</summary>
    public ListItem? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count
        ? _items[_selectedIndex]
        : null;

    /// <summary>Read-only view of all items.</summary>
    public IReadOnlyList<ListItem> Items => _items;

    public List AddItem(string text, object? tag = null)
    {
        _items.Add(new ListItem(text, tag));
        if (_selectedIndex < 0) _selectedIndex = 0;
        return this;
    }

    public List AddItems(IEnumerable<string> texts)
    {
        foreach (var t in texts) AddItem(t);
        return this;
    }

    public void MoveUp()
    {
        if (_selectedIndex > 0) _selectedIndex--;
        EnsureVisible();
    }

    public void MoveDown()
    {
        if (_selectedIndex < _items.Count - 1) _selectedIndex++;
        EnsureVisible();
    }

    private void EnsureVisible()
    {
        if (_selectedIndex < _scrollOffset)
            _scrollOffset = _selectedIndex;
        else if (_selectedIndex >= _scrollOffset + Bounds.Height)
            _scrollOffset = _selectedIndex - Bounds.Height + 1;
    }

    public override void Render(Canvas canvas)
    {
        if (!IsVisible || Bounds.IsEmpty) return;

        int visibleRows = Bounds.Height;
        for (int i = 0; i < visibleRows; i++)
        {
            int itemIndex = _scrollOffset + i;
            int row = Bounds.Y + i;

            if (itemIndex >= _items.Count)
            {
                // Blank the remaining rows
                canvas.DrawHorizontalLine(Bounds.X, row, Bounds.Width, ' ', ItemStyle);
                continue;
            }

            var item = _items[itemIndex];
            bool selected = itemIndex == _selectedIndex;
            var style = selected ? SelectedStyle : ItemStyle;

            string text = item.Text.Length > Bounds.Width
                ? item.Text[..Bounds.Width]
                : item.Text.PadRight(Bounds.Width);

            canvas.DrawText(Bounds.X, row, text.AsSpan(), style);
        }
    }
}

/// <summary>A single item in a <see cref="List"/> widget.</summary>
public sealed class ListItem
{
    public ListItem(string text, object? tag = null)
    {
        Text = text;
        Tag = tag;
    }

    public string Text { get; }
    public object? Tag { get; }
}
