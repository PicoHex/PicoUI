namespace PicoTUI.Widgets;

/// <summary>
/// Renders wrapped or truncated multi-line text inside its bounds.
/// </summary>
public sealed class Paragraph : Widget
{
    private readonly List<string> _lines = [];

    /// <summary>Visual style for the text.</summary>
    public Style Style { get; set; } = Style.Default;

    /// <summary>Whether to wrap long lines instead of truncating.</summary>
    public bool Wrap { get; set; } = true;

    /// <summary>Replaces all text with the given lines.</summary>
    public Paragraph WithLines(params string[] lines)
    {
        _lines.Clear();
        _lines.AddRange(lines);
        return this;
    }

    /// <summary>Appends a line of text.</summary>
    public Paragraph AddLine(string line) { _lines.Add(line); return this; }

    /// <summary>Replaces all text with a single block of text (split on newlines).</summary>
    public Paragraph WithText(string text)
    {
        _lines.Clear();
        foreach (var line in text.Split('\n'))
            _lines.Add(line.TrimEnd('\r'));
        return this;
    }

    public override void Render(Canvas canvas)
    {
        if (!IsVisible || Bounds.IsEmpty) return;

        int y = Bounds.Y;
        int maxWidth = Bounds.Width;

        foreach (string rawLine in _lines)
        {
            if (y >= Bounds.Bottom) break;

            if (!Wrap || rawLine.Length <= maxWidth)
            {
                canvas.DrawText(Bounds.X, y, rawLine.AsSpan(), Style);
                y++;
            }
            else
            {
                // Wrap into multiple visual rows
                int start = 0;
                while (start < rawLine.Length && y < Bounds.Bottom)
                {
                    int end = Math.Min(start + maxWidth, rawLine.Length);
                    canvas.DrawText(Bounds.X, y, rawLine.AsSpan(start, end - start), Style);
                    start = end;
                    y++;
                }
            }
        }
    }
}
