namespace PicoTui.Layout;

public readonly record struct LayoutItem(
    IComponent Component,
    int Basis = 0,
    int Grow = 0,
    int Shrink = 1,
    int MinSize = 0,
    int MaxSize = int.MaxValue,
    Func<int, int, bool>? Visible = null
);
