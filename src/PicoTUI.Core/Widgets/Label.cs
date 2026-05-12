namespace PicoTUI.Widgets;

/// <summary>
/// A simple text label that renders a string at a fixed position.
/// </summary>
public sealed class Label : Widget
{
    /// <summary>The text to display.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Visual style (foreground, background, attributes).</summary>
    public Style Style { get; set; } = Style.Default;

    /// <summary>Text alignment within <see cref="Widget.Bounds"/>.</summary>
    public Alignment Alignment { get; set; } = Alignment.Left;

    public override void Render(Canvas canvas)
    {
        if (!IsVisible || Bounds.IsEmpty) return;

        var text = Text;
        int available = Bounds.Width;

        // Truncate or align
        string display = text.Length > available
            ? text[..available]
            : Alignment switch
            {
                Alignment.Center => text.PadLeft((available + text.Length) / 2).PadRight(available),
                Alignment.Right => text.PadLeft(available),
                _ => text
            };

        canvas.DrawText(Bounds.X, Bounds.Y, display.AsSpan(), Style);
    }
}

/// <summary>Horizontal text alignment options.</summary>
public enum Alignment
{
    Left,
    Center,
    Right
}
