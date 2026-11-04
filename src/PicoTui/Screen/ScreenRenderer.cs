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

    /// <summary>
    /// The frame this renderer last drew (null before the first one). Guard
    /// surface for F9-1: after two frames the tracked buffer must be the frame
    /// that is on screen, otherwise the diff would be computed against a frame
    /// that was never painted.
    /// </summary>
    internal ScreenBuffer? PreviousFrame => _prev;

    /// <summary>Neutralizes control characters coming from rendered content:
    /// untrusted text (LLM/tool output) must not inject terminal escapes.</summary>
    internal static int NeutralizeRune(int rune) =>
        rune <= char.MaxValue && char.IsControl((char)rune) ? ' ' : rune;

    public void Render(IComponent root)
    {
        var width = _terminal.Columns;
        var height = _terminal.Rows;
        // 05-C2: the height goes down to the root, so a vertical stack can honour
        // Grow/Shrink/Basis and a scroll view can clip to its viewport. Components
        // that only implement Render(int) keep working through the interface's
        // default member.
        var lines = root.Render(width, height);

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
            // F9-1 (05-C1 residual): the first frame needs the same erase+home as a
            // full redraw. TuiLifecycle.Enter switches to the alternate screen, which
            // inherits whatever cursor row the shell left behind, and ED(2J) never
            // moves the cursor — so an unpositioned first frame starts drawing at the
            // inherited row, wraps, and leaves every later absolute-CUP row offset for
            // the rest of the session (the diff only repaints changed rows).
            _terminal.Write(
                AnsiEncoder.WrapSynchronized("\x1b[2J\x1b[H" + RenderFull(next))
            );
            _prev = next.CopyForDiff();
            return;
        }

        var diff = DiffRenderer.Compute(_prev, next);
        if (diff.FirstDiffLine < 0)
            return; // identical frame: _prev already equals next, a clone would be wasted

        string body;
        if (diff.NeedsFullRedraw)
        {
            // clear screen + full re-render (width changed or >50% rows changed).
            // 05-C1: ED(2J) only erases — per ECMA-48/xterm it never moves the
            // cursor, so the frame would start drawing wherever the previous one
            // ended (bottom-right ⇒ wrap-around, permanent scrambling). CUP home
            // must follow immediately; erase-then-home keeps the home as the last
            // positioning op before the body and makes the intent (“start at 1;1”)
            // greppable in the emitted bytes.
            body = "\x1b[2J\x1b[H" + RenderFull(next);
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
