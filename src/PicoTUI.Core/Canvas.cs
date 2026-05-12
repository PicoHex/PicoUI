namespace PicoTUI;

/// <summary>
/// A virtual screen buffer that widgets draw into. Supports double-buffering:
/// callers fill the <see cref="Canvas"/> with cells, then call
/// <see cref="Renderer.Render"/> which diffs against the previous frame and
/// only emits ANSI sequences for changed cells.
/// </summary>
public sealed class Canvas
{
    private Cell[] _cells;
    private int _width;
    private int _height;

    /// <summary>Creates a canvas of the given size.</summary>
    public Canvas(int width, int height)
    {
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        _width = width;
        _height = height;
        _cells = new Cell[width * height];
        Array.Fill(_cells, Cell.Empty);
    }

    public int Width => _width;
    public int Height => _height;
    public Rect Bounds => new(0, 0, _width, _height);

    // ── Cell access ──────────────────────────────────────────────────────────

    /// <summary>Returns the cell at the given position, or <see cref="Cell.Empty"/> if out of bounds.</summary>
    public Cell GetCell(int x, int y)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height) return Cell.Empty;
        return _cells[y * _width + x];
    }

    /// <summary>Sets the cell at the given position (silently ignores out-of-bounds).</summary>
    public void SetCell(int x, int y, Cell cell)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height) return;
        _cells[y * _width + x] = cell;
    }

    /// <summary>Returns a span over the entire internal cell array (row-major order).</summary>
    internal ReadOnlySpan<Cell> Cells => _cells;

    // ── Resize ───────────────────────────────────────────────────────────────

    /// <summary>Resizes the canvas, preserving as much existing content as possible.</summary>
    public void Resize(int width, int height)
    {
        if (width == _width && height == _height) return;

        var newCells = new Cell[width * height];
        Array.Fill(newCells, Cell.Empty);

        int copyW = Math.Min(width, _width);
        int copyH = Math.Min(height, _height);
        for (int row = 0; row < copyH; row++)
            for (int col = 0; col < copyW; col++)
                newCells[row * width + col] = _cells[row * _width + col];

        _cells = newCells;
        _width = width;
        _height = height;
    }

    // ── Drawing primitives ───────────────────────────────────────────────────

    /// <summary>Fills the entire canvas with blank cells using the default style.</summary>
    public void Clear() => Clear(Style.Default);

    /// <summary>Fills the entire canvas with blank cells using the given style.</summary>
    public void Clear(Style style)
    {
        var blank = new Cell(' ', style);
        Array.Fill(_cells, blank);
    }

    /// <summary>Fills the given rectangle with blank cells using the given style.</summary>
    public void Fill(Rect rect, Style style)
    {
        var clip = rect.Intersect(Bounds);
        if (clip.IsEmpty) return;
        var blank = new Cell(' ', style);
        for (int row = clip.Y; row < clip.Bottom; row++)
            for (int col = clip.X; col < clip.Right; col++)
                _cells[row * _width + col] = blank;
    }

    /// <summary>Draws a text string at the given position, clipping to canvas bounds.</summary>
    public void DrawText(int x, int y, ReadOnlySpan<char> text, Style style)
    {
        if (y < 0 || y >= _height) return;
        int col = x;
        foreach (char ch in text)
        {
            if (col >= _width) break;
            if (col >= 0)
                _cells[y * _width + col] = new Cell(ch, style);
            col++;
        }
    }

    /// <summary>Draws a single character at (x, y).</summary>
    public void DrawChar(int x, int y, char ch, Style style)
    {
        if (x < 0 || x >= _width || y < 0 || y >= _height) return;
        _cells[y * _width + x] = new Cell(ch, style);
    }

    /// <summary>Draws a horizontal line of the given character.</summary>
    public void DrawHorizontalLine(int x, int y, int length, char ch, Style style)
    {
        if (y < 0 || y >= _height) return;
        int end = Math.Min(x + length, _width);
        for (int col = Math.Max(x, 0); col < end; col++)
            _cells[y * _width + col] = new Cell(ch, style);
    }

    /// <summary>Draws a vertical line of the given character.</summary>
    public void DrawVerticalLine(int x, int y, int length, char ch, Style style)
    {
        if (x < 0 || x >= _width) return;
        int end = Math.Min(y + length, _height);
        for (int row = Math.Max(y, 0); row < end; row++)
            _cells[row * _width + x] = new Cell(ch, style);
    }

    /// <summary>Draws a border around the given rectangle using the specified border set.</summary>
    public void DrawBorder(Rect rect, in BorderSet border, Style style)
    {
        if (rect.Width < 2 || rect.Height < 2) return;

        int x = rect.X, y = rect.Y, w = rect.Width, h = rect.Height;

        // Corners
        DrawChar(x, y, border.TopLeft, style);
        DrawChar(x + w - 1, y, border.TopRight, style);
        DrawChar(x, y + h - 1, border.BottomLeft, style);
        DrawChar(x + w - 1, y + h - 1, border.BottomRight, style);

        // Edges
        DrawHorizontalLine(x + 1, y, w - 2, border.Top, style);
        DrawHorizontalLine(x + 1, y + h - 1, w - 2, border.Bottom, style);
        DrawVerticalLine(x, y + 1, h - 2, border.Left, style);
        DrawVerticalLine(x + w - 1, y + 1, h - 2, border.Right, style);
    }
}
