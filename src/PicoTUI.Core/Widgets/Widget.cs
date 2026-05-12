namespace PicoTUI.Widgets;

/// <summary>
/// Base class for all TUI widgets. A widget knows its bounding <see cref="Rect"/>
/// and can render itself into a <see cref="Canvas"/>.
/// </summary>
public abstract class Widget
{
    /// <summary>The area this widget occupies on the canvas.</summary>
    public Rect Bounds { get; set; }

    /// <summary>Whether the widget should be rendered.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Renders the widget into the given canvas.</summary>
    public abstract void Render(Canvas canvas);
}
