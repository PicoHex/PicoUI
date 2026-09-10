namespace PicoTui.Tests.Screen;

public sealed class DiffRendererTests
{
    private static ScreenBuffer Make(int rows, int cols, char fill = ' ')
    {
        var b = new ScreenBuffer();
        b.Resize(rows, cols);
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
            b.Set(r, c, new Cell(fill, default));
        return b;
    }

    [Test]
    public async Task IdenticalBuffers_NoDiff()
    {
        var a = Make(3, 3);
        var b = Make(3, 3);
        var d = DiffRenderer.Compute(a, b);
        await Assert.That(d.FirstDiffLine).IsEqualTo(-1);
        await Assert.That(d.LastDiffLine).IsEqualTo(-1);
        await Assert.That(d.NeedsFullRedraw).IsFalse();
    }

    [Test]
    public async Task SingleLineChanged_ReportsRange()
    {
        var a = Make(5, 3);
        var b = Make(5, 3);
        b.Set(2, 0, new Cell('X', default));
        var d = DiffRenderer.Compute(a, b);
        await Assert.That(d.FirstDiffLine).IsEqualTo(2);
        await Assert.That(d.LastDiffLine).IsEqualTo(2);
        await Assert.That(d.NeedsFullRedraw).IsFalse();
    }

    [Test]
    public async Task MostLinesChanged_FullRedraw()
    {
        var a = Make(10, 3);
        var b = Make(10, 3);
        for (var r = 0; r < 6; r++)
            b.Set(r, 0, new Cell('X', default)); // >50% of 10 rows
        var d = DiffRenderer.Compute(a, b);
        await Assert.That(d.NeedsFullRedraw).IsTrue();
    }

    [Test]
    public async Task WidthChanged_FullRedraw()
    {
        var a = Make(3, 3);
        var b = Make(3, 5);
        var d = DiffRenderer.Compute(a, b);
        await Assert.That(d.WidthChanged).IsTrue();
        await Assert.That(d.NeedsFullRedraw).IsTrue();
    }
}
