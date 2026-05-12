namespace PicoTUI.Core.Tests;

public class CanvasTests
{
    // ── Construction ──────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_InitialisedWithBlankCells()
    {
        var canvas = new Canvas(5, 3);
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 5; x++)
                Assert.Equal(Cell.Empty, canvas.GetCell(x, y));
    }

    [Fact]
    public void Canvas_OutOfBounds_ReturnsEmpty()
    {
        var canvas = new Canvas(5, 3);
        Assert.Equal(Cell.Empty, canvas.GetCell(-1, 0));
        Assert.Equal(Cell.Empty, canvas.GetCell(5, 0));
        Assert.Equal(Cell.Empty, canvas.GetCell(0, 3));
    }

    [Fact]
    public void Canvas_SetCell_OutOfBounds_DoesNotThrow()
    {
        var canvas = new Canvas(5, 3);
        canvas.SetCell(-1, 0, new Cell('X', Style.Default)); // should not throw
        canvas.SetCell(5, 0, new Cell('X', Style.Default));
        canvas.SetCell(0, 3, new Cell('X', Style.Default));
    }

    // ── DrawText ──────────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_DrawText_WritesCharacters()
    {
        var canvas = new Canvas(20, 5);
        canvas.DrawText(2, 1, "Hello".AsSpan(), Style.Default);

        Assert.Equal('H', canvas.GetCell(2, 1).Character);
        Assert.Equal('e', canvas.GetCell(3, 1).Character);
        Assert.Equal('l', canvas.GetCell(4, 1).Character);
        Assert.Equal('l', canvas.GetCell(5, 1).Character);
        Assert.Equal('o', canvas.GetCell(6, 1).Character);
    }

    [Fact]
    public void Canvas_DrawText_ClipsAtRightEdge()
    {
        var canvas = new Canvas(5, 3);
        canvas.DrawText(3, 0, "ABCDE".AsSpan(), Style.Default);

        Assert.Equal('A', canvas.GetCell(3, 0).Character);
        Assert.Equal('B', canvas.GetCell(4, 0).Character);
        // 'C', 'D', 'E' are out of bounds and should not appear
        Assert.Equal(' ', canvas.GetCell(0, 0).Character); // untouched
    }

    [Fact]
    public void Canvas_DrawText_NegativeX_SkipsOffscreen()
    {
        var canvas = new Canvas(10, 3);
        canvas.DrawText(-2, 0, "ABCDE".AsSpan(), Style.Default);

        // A, B are off-screen, C lands at x=0, D at x=1, E at x=2
        Assert.Equal('C', canvas.GetCell(0, 0).Character);
        Assert.Equal('D', canvas.GetCell(1, 0).Character);
        Assert.Equal('E', canvas.GetCell(2, 0).Character);
    }

    [Fact]
    public void Canvas_DrawText_OutOfVerticalBounds_NoEffect()
    {
        var canvas = new Canvas(10, 3);
        canvas.DrawText(0, -1, "ABC".AsSpan(), Style.Default);
        canvas.DrawText(0, 3, "ABC".AsSpan(), Style.Default);
        // No cells should have changed
        for (int x = 0; x < 10; x++)
            Assert.Equal(Cell.Empty, canvas.GetCell(x, 0));
    }

    // ── DrawChar ──────────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_DrawChar_SetsCell()
    {
        var canvas = new Canvas(5, 5);
        var style = Style.Default.WithForeground(Color.Red);
        canvas.DrawChar(2, 2, '★', style);
        var cell = canvas.GetCell(2, 2);
        Assert.Equal('★', cell.Character);
        Assert.Equal(style, cell.Style);
    }

    // ── DrawHorizontalLine ────────────────────────────────────────────────────

    [Fact]
    public void Canvas_DrawHorizontalLine_FillsRow()
    {
        var canvas = new Canvas(10, 5);
        canvas.DrawHorizontalLine(1, 2, 5, '─', Style.Default);
        for (int x = 1; x <= 5; x++)
            Assert.Equal('─', canvas.GetCell(x, 2).Character);
        Assert.Equal(' ', canvas.GetCell(0, 2).Character);
        Assert.Equal(' ', canvas.GetCell(6, 2).Character);
    }

    // ── DrawVerticalLine ──────────────────────────────────────────────────────

    [Fact]
    public void Canvas_DrawVerticalLine_FillsColumn()
    {
        var canvas = new Canvas(10, 10);
        canvas.DrawVerticalLine(3, 1, 4, '│', Style.Default);
        for (int y = 1; y <= 4; y++)
            Assert.Equal('│', canvas.GetCell(3, y).Character);
        Assert.Equal(' ', canvas.GetCell(3, 0).Character);
        Assert.Equal(' ', canvas.GetCell(3, 5).Character);
    }

    // ── Fill ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_Fill_FillsRegion()
    {
        var canvas = new Canvas(10, 10);
        var style = Style.Default.WithBackground(Color.Blue);
        canvas.Fill(new Rect(2, 2, 3, 3), style);

        for (int y = 2; y < 5; y++)
            for (int x = 2; x < 5; x++)
                Assert.Equal(style, canvas.GetCell(x, y).Style);

        // Outside region is untouched
        Assert.Equal(Style.Default, canvas.GetCell(1, 1).Style);
        Assert.Equal(Style.Default, canvas.GetCell(5, 5).Style);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_Clear_ResetsAllCells()
    {
        var canvas = new Canvas(5, 5);
        canvas.DrawText(0, 0, "Hello".AsSpan(), Style.Default);
        canvas.Clear();
        for (int x = 0; x < 5; x++)
            Assert.Equal(Cell.Empty, canvas.GetCell(x, 0));
    }

    // ── DrawBorder ────────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_DrawBorder_Single_DrawsCorners()
    {
        var canvas = new Canvas(10, 5);
        canvas.DrawBorder(new Rect(0, 0, 10, 5), BorderSet.Single, Style.Default);

        Assert.Equal('┌', canvas.GetCell(0, 0).Character);
        Assert.Equal('┐', canvas.GetCell(9, 0).Character);
        Assert.Equal('└', canvas.GetCell(0, 4).Character);
        Assert.Equal('┘', canvas.GetCell(9, 4).Character);
    }

    [Fact]
    public void Canvas_DrawBorder_Single_DrawsEdges()
    {
        var canvas = new Canvas(10, 5);
        canvas.DrawBorder(new Rect(0, 0, 10, 5), BorderSet.Single, Style.Default);

        // Top/bottom edges
        for (int x = 1; x < 9; x++)
        {
            Assert.Equal('─', canvas.GetCell(x, 0).Character);
            Assert.Equal('─', canvas.GetCell(x, 4).Character);
        }

        // Left/right edges
        for (int y = 1; y < 4; y++)
        {
            Assert.Equal('│', canvas.GetCell(0, y).Character);
            Assert.Equal('│', canvas.GetCell(9, y).Character);
        }
    }

    // ── Resize ────────────────────────────────────────────────────────────────

    [Fact]
    public void Canvas_Resize_Larger_PreservesContent()
    {
        var canvas = new Canvas(5, 3);
        canvas.DrawChar(2, 1, 'X', Style.Default);
        canvas.Resize(10, 6);

        Assert.Equal(10, canvas.Width);
        Assert.Equal(6, canvas.Height);
        Assert.Equal('X', canvas.GetCell(2, 1).Character);
    }

    [Fact]
    public void Canvas_Resize_Smaller_ClipsContent()
    {
        var canvas = new Canvas(10, 6);
        canvas.DrawChar(8, 5, 'X', Style.Default);
        canvas.Resize(5, 3);

        Assert.Equal(5, canvas.Width);
        Assert.Equal(3, canvas.Height);
        Assert.Equal(Cell.Empty, canvas.GetCell(4, 2)); // content that was clipped
    }

    [Fact]
    public void Canvas_Resize_SameSize_NoChange()
    {
        var canvas = new Canvas(5, 3);
        canvas.DrawChar(1, 1, 'A', Style.Default);
        canvas.Resize(5, 3); // no-op
        Assert.Equal('A', canvas.GetCell(1, 1).Character);
    }
}
