namespace PicoTUI.Widgets;

/// <summary>
/// A horizontal progress bar that fills from left to right based on a ratio (0.0–1.0).
/// </summary>
public sealed class Gauge : Widget
{
    /// <summary>Fill ratio in the range [0, 1].</summary>
    public double Ratio { get; set; } = 0.0;

    /// <summary>Style for the filled portion of the gauge.</summary>
    public Style FilledStyle { get; set; } = new Style(Color.Black, Color.Green, TextAttributes.None);

    /// <summary>Style for the unfilled (empty) portion.</summary>
    public Style EmptyStyle { get; set; } = Style.Default;

    /// <summary>Optional label shown in the centre of the gauge.</summary>
    public string? Label { get; set; }

    /// <summary>Style for the label text.</summary>
    public Style LabelStyle { get; set; } = new Style(Color.White, Color.Default, TextAttributes.Bold);

    public override void Render(Canvas canvas)
    {
        if (!IsVisible || Bounds.IsEmpty) return;

        double clampedRatio = Math.Clamp(Ratio, 0.0, 1.0);
        int filledWidth = (int)Math.Round(clampedRatio * Bounds.Width);

        for (int row = Bounds.Y; row < Bounds.Bottom; row++)
        {
            // Filled cells
            for (int col = Bounds.X; col < Bounds.X + filledWidth; col++)
                canvas.DrawChar(col, row, ' ', FilledStyle);

            // Empty cells
            for (int col = Bounds.X + filledWidth; col < Bounds.Right; col++)
                canvas.DrawChar(col, row, ' ', EmptyStyle);
        }

        // Draw centred label on the middle row
        if (!string.IsNullOrEmpty(Label))
        {
            int midRow = Bounds.Y + Bounds.Height / 2;
            string text = Label;
            int textX = Bounds.X + Math.Max(0, (Bounds.Width - text.Length) / 2);
            canvas.DrawText(textX, midRow, text.AsSpan(), LabelStyle);
        }
    }
}
