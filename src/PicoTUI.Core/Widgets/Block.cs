namespace PicoTUI.Widgets;

/// <summary>
/// Draws a rectangular border (box) with an optional title.
/// The inner area is available for child widgets to draw into.
/// </summary>
public sealed class Block : Widget
{
    /// <summary>Optional title displayed in the top border.</summary>
    public string? Title { get; set; }

    /// <summary>Title alignment within the top border.</summary>
    public Alignment TitleAlignment { get; set; } = Alignment.Left;

    /// <summary>Border character set.</summary>
    public BorderSet BorderSet { get; set; } = BorderSet.Single;

    /// <summary>Style for the border and title.</summary>
    public Style BorderStyle { get; set; } = Style.Default;

    /// <summary>Style used to fill the interior of the block.</summary>
    public Style InnerStyle { get; set; } = Style.Default;

    /// <summary>
    /// Returns the inner rectangle (excluding the border cells).
    /// Use this as the <see cref="Widget.Bounds"/> for child widgets.
    /// </summary>
    public Rect InnerBounds => Bounds.Shrink(1);

    public override void Render(Canvas canvas)
    {
        if (!IsVisible || Bounds.IsEmpty) return;

        // Fill interior
        var inner = InnerBounds;
        if (!inner.IsEmpty)
            canvas.Fill(inner, InnerStyle);

        // Draw border
        canvas.DrawBorder(Bounds, BorderSet, BorderStyle);

        // Draw title in top border
        if (!string.IsNullOrEmpty(Title) && Bounds.Width > 2)
        {
            int innerWidth = Bounds.Width - 2; // exclude corner chars
            string text = Title.Length > innerWidth ? Title[..innerWidth] : Title;

            int titleX = TitleAlignment switch
            {
                Alignment.Center => Bounds.X + 1 + (innerWidth - text.Length) / 2,
                Alignment.Right => Bounds.X + 1 + innerWidth - text.Length,
                _ => Bounds.X + 1
            };

            canvas.DrawText(titleX, Bounds.Y, text.AsSpan(), BorderStyle);
        }
    }
}
