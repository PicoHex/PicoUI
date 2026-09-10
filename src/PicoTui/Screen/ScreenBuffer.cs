namespace PicoTui.Screen;

public sealed class Line
{
    public required Cell[] Cells { get; init; }
    public int Width => Cells.Length;
}

public sealed class ScreenBuffer
{
    private Line[] _lines = [];

    public int Rows => _lines.Length;
    public int Cols => _lines.Length == 0 ? 0 : _lines[0].Width;

    public void Resize(int rows, int cols)
    {
        var next = new Line[rows];
        for (var r = 0; r < rows; r++)
        {
            var cells = new Cell[cols];
            if (r < _lines.Length)
                Array.Copy(_lines[r].Cells, cells, Math.Min(cols, _lines[r].Width));
            next[r] = new Line { Cells = cells };
        }
        _lines = next;
    }

    public Cell Get(int row, int col) => _lines[row].Cells[col];

    public void Set(int row, int col, Cell cell) => _lines[row].Cells[col] = cell;

    /// <summary>
    /// Returns the internal line without copying (v1 perf tradeoff). Callers must
    /// treat the returned line as read-only — mutating it corrupts the buffer.
    /// </summary>
    public Line GetLine(int row) => _lines[row];

    public ScreenBuffer CopyForDiff()
    {
        var copy = new ScreenBuffer();
        var rows = _lines;
        var next = new Line[rows.Length];
        for (var r = 0; r < rows.Length; r++)
            next[r] = new Line { Cells = (Cell[])rows[r].Cells.Clone() };
        copy._lines = next;
        return copy;
    }
}
