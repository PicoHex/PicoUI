namespace PicoTui.Screen;

/// <summary>
/// Wires the differential-rendering pipeline: root render → cell buffer →
/// diff against the previous frame → minimal ANSI output (synchronized).
/// This is the only writer to the terminal during normal frames.
/// </summary>
public sealed class ScreenRenderer
{
    private readonly ITerminal _terminal;
    private ScreenBuffer? _prev;

    public ScreenRenderer(ITerminal terminal) => _terminal = terminal;

    public void Render(IComponent root)
    {
        var width = _terminal.Columns;
        var height = _terminal.Rows;
        var lines = root.Render(width);

        var next = new ScreenBuffer();
        next.Resize(height, width);
        for (var r = 0; r < height; r++)
        {
            var text = r < lines.Length ? lines[r] : "";
            if (text.Length > width)
                text = text[..width];
            for (var c = 0; c < text.Length; c++)
                next.Set(r, c, new Cell(text[c], default));
        }

        if (_prev is null)
        {
            // First render: full output, no scrollback clear
            _terminal.Write(AnsiEncoder.WrapSynchronized(RenderFull(next)));
            _prev = next.CopyForDiff();
            return;
        }

        var diff = DiffRenderer.Compute(_prev, next);
        if (diff.FirstDiffLine < 0)
        {
            _prev = next.CopyForDiff();
            return;
        }

        string body;
        if (diff.NeedsFullRedraw)
        {
            // clear screen + full re-render (width changed or >50% rows changed)
            body = "\x1b[2J" + RenderFull(next);
        }
        else
        {
            // position to first changed line, write changed lines with clear-to-end
            var sb = new StringBuilder();
            sb.Append($"\x1b[{diff.FirstDiffLine + 1};1H");
            for (var r = diff.FirstDiffLine; r <= diff.LastDiffLine; r++)
            {
                if (r > diff.FirstDiffLine)
                    sb.Append("\r\n");
                sb.Append(AnsiEncoder.EncodeLine(next.GetLine(r), clearToEnd: true));
            }
            body = sb.ToString();
        }

        _terminal.Write(AnsiEncoder.WrapSynchronized(body));
        _prev = next.CopyForDiff();
    }

    private static string RenderFull(ScreenBuffer buffer)
    {
        var sb = new StringBuilder();
        for (var r = 0; r < buffer.Rows; r++)
        {
            if (r > 0)
                sb.Append("\r\n");
            sb.Append(AnsiEncoder.EncodeLine(buffer.GetLine(r), clearToEnd: true));
        }
        return sb.ToString();
    }
}
