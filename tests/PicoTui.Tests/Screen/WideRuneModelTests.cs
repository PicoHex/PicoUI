namespace PicoTui.Tests.Screen;

/// <summary>
/// Root fix for CJK/emoji alignment: the screen model is column based. A wide
/// rune occupies two columns (its cell plus a continuation marker), combining
/// marks occupy none, and encoding skips the continuation so the emitted line
/// covers exactly the buffer's column count.
/// </summary>
public sealed class WideRuneModelTests
{
    private sealed class FixedLines(string[] lines) : IComponent
    {
        public string[] Render(int width) => lines;

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    /// <summary>Visible width of terminal output with CSI/OSC sequences removed.</summary>
    private static int VisibleWidth(string output) => AnsiTestOutput.VisibleWidth(output);

    [Test]
    public async Task Resize_FillsBufferWithBlankCells()
    {
        var b = new ScreenBuffer();
        b.Resize(1, 3);

        await Assert.That(b.Get(0, 0)).IsEqualTo(Cell.Blank);
    }

    [Test]
    public async Task WriteRune_WideRune_OccupiesTwoColumns()
    {
        var b = new ScreenBuffer();
        b.Resize(1, 4);

        var used = b.WriteRune(0, 0, '中', default);

        await Assert.That(used).IsEqualTo(2);
        await Assert.That(b.Get(0, 0).Rune).IsEqualTo('中');
        await Assert.That(b.Get(0, 1).Continuation).IsTrue();
        await Assert.That(b.Get(0, 2)).IsEqualTo(Cell.Blank);
    }

    [Test]
    public async Task WriteRune_WideRuneDoesNotFit_ReturnsZero()
    {
        var b = new ScreenBuffer();
        b.Resize(1, 3);

        var used = b.WriteRune(0, 2, '中', default);

        await Assert.That(used).IsEqualTo(0);
        await Assert.That(b.Get(0, 2)).IsEqualTo(Cell.Blank);
    }

    [Test]
    public async Task EncodeLine_SkipsContinuationCells()
    {
        var b = new ScreenBuffer();
        b.Resize(1, 4);
        b.WriteRune(0, 0, '中', default);
        b.WriteRune(0, 2, '文', default);

        var line = b.GetLine(0);
        var encoded = AnsiEncoder.EncodeLine(line, clearToEnd: false);

        await Assert.That(AnsiTestOutput.Strip(encoded)).IsEqualTo("中文");
    }

    [Test]
    public async Task ScreenRenderer_WideRunes_CoverExactColumnCount()
    {
        var vt = new VirtualTerminal(4, 1);

        new ScreenRenderer(vt).Render(new FixedLines(["中文"]));

        await Assert.That(VisibleWidth(vt.Output)).IsEqualTo(4);
    }

    [Test]
    public async Task ScreenRenderer_CombiningMark_DoesNotConsumeAColumn()
    {
        var vt = new VirtualTerminal(2, 1);

        new ScreenRenderer(vt).Render(new FixedLines(["e\u0301x"]));

        await Assert.That(vt.Output.Contains("ex")).IsTrue();
        await Assert.That(VisibleWidth(vt.Output)).IsEqualTo(2);
    }
}
