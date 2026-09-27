namespace PicoTui.Screen;

/// <summary>
/// Wires the differential-rendering pipeline: root render → cell buffer →
/// diff against the previous frame → minimal ANSI output (synchronized).
/// This is the only writer to the terminal during normal frames.
/// </summary>
/// <remarks>Zero-width runes (combining marks, joiners, variation selectors) get
/// no cell, so the emitted glyphs match the column model (a ZWJ emoji sequence
/// renders as its separate emoji rather than a ligature).</remarks>
public sealed class ScreenRenderer
{
    private readonly ITerminal _terminal;
    private ScreenBuffer? _prev;

    public ScreenRenderer(ITerminal terminal) => _terminal = terminal;

    /// <summary>Neutralizes control characters coming from rendered content:
    /// untrusted text (LLM/tool output) must not inject terminal escapes.</summary>
    internal static int NeutralizeRune(int rune) =>
        rune <= char.MaxValue && char.IsControl((char)rune) ? ' ' : rune;

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
            var col = 0;
            foreach (var rune in text.EnumerateRunes())
            {
                if (col >= width)
                    break;
                if (WidthTable.IsZeroWidth(rune.Value))
                    continue; // zero-width rune: no cell of its own
                var used = next.WriteRune(r, col, NeutralizeRune(rune.Value), default);
                if (used == 0)
                    break; // a wide rune with one column left cannot be shown
                col += used;
            }
            for (; col < width; col++)
                next.Set(r, col, Cell.Blank);
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
            return; // identical frame: _prev already equals next, a clone would be wasted

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
