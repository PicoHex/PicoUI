using System.Text;

namespace PicoTUI;

/// <summary>
/// Renders a <see cref="Canvas"/> to the terminal using a diff algorithm:
/// only cells that changed since the last frame are written, minimising I/O.
/// </summary>
public sealed class Renderer : IDisposable
{
    private Canvas _current;
    private Canvas _previous;
    private readonly StringBuilder _sb = new(4096);
    private bool _forceFull = true;

    /// <summary>Creates a renderer sized to the current terminal.</summary>
    public Renderer()
    {
        var sz = Terminal.GetSize();
        _current = new Canvas(sz.Width, sz.Height);
        _previous = new Canvas(sz.Width, sz.Height);
    }

    /// <summary>
    /// Returns the writable canvas for the current frame.
    /// Widgets should draw into this canvas, then call <see cref="Render"/>.
    /// </summary>
    public Canvas Canvas => _current;

    /// <summary>
    /// Notifies the renderer that the terminal was resized.
    /// The next call to <see cref="Render"/> will perform a full redraw.
    /// </summary>
    public void Resize(int width, int height)
    {
        _current.Resize(width, height);
        _previous.Resize(width, height);
        _forceFull = true;
    }

    /// <summary>
    /// Notifies the renderer that the terminal was resized to the current terminal size.
    /// </summary>
    public void Resize() => Resize(Terminal.GetSize().Width, Terminal.GetSize().Height);

    /// <summary>
    /// Flushes the current canvas to the terminal, writing only changed cells.
    /// After this call the current canvas is cleared (filled with blank cells)
    /// so that widgets can draw the next frame.
    /// </summary>
    public void Render()
    {
        _sb.Clear();
        var currentCells = _current.Cells;
        var previousCells = _previous.Cells;

        int width = _current.Width;
        int height = _current.Height;

        Style lastStyle = default;
        bool styleSet = false;

        var ab = new AnsiBuilder(_sb);

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                int idx = row * width + col;
                if (idx >= currentCells.Length) break;

                var cell = currentCells[idx];
                bool prevValid = idx < previousCells.Length;
                var prevCell = prevValid ? previousCells[idx] : new Cell('\0', default);

                if (!_forceFull && cell == prevCell) continue;

                // Move cursor to this cell's position
                ab.AppendMoveTo(col + 1, row + 1);

                // Emit style change only if needed
                if (!styleSet || cell.Style != lastStyle)
                {
                    ab.AppendStyle(cell.Style);
                    lastStyle = cell.Style;
                    styleSet = true;
                }

                _sb.Append(cell.Character);
            }
        }

        // Reset attributes at end of frame
        _sb.Append("\x1B[0m");

        // Write all at once
        Terminal.Out.Write(_sb);
        Terminal.Flush();

        // Swap buffers: copy current → previous, then clear current
        var temp = _previous;
        _previous = _current;
        _current = temp;
        _current.Clear();

        _forceFull = false;
    }

    /// <summary>Forces a full redraw on the next <see cref="Render"/> call.</summary>
    public void Invalidate() => _forceFull = true;

    public void Dispose()
    {
        Terminal.ResetAttributes();
        Terminal.Flush();
    }
}
