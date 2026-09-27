using PicoTui.Tests.Screen;

namespace PicoTui.Tests.Input;

/// <summary>
/// Format controls carry no width: joiners, variation selectors, zero-width
/// spaces and the BOM must not consume a column (they used to add one each,
/// which drifted every line containing emoji sequences).
/// </summary>
public sealed class ZeroWidthRuneTests
{
    [Test]
    public async Task ZeroWidthJoiner_OccupiesNoColumn()
    {
        await Assert.That(WidthTable.VisibleWidth("a\u200db")).IsEqualTo(2);
    }

    [Test]
    public async Task OtherFormatControls_OccupyNoColumn()
    {
        await Assert.That(WidthTable.VisibleWidth("a\u200cb")).IsEqualTo(2); // ZWNJ
        await Assert.That(WidthTable.VisibleWidth("a\u200bb")).IsEqualTo(2); // ZWSP
        await Assert.That(WidthTable.VisibleWidth("a\u2060b")).IsEqualTo(2); // word joiner
        await Assert.That(WidthTable.VisibleWidth("a\ufe0fb")).IsEqualTo(2); // VS16
        await Assert.That(WidthTable.VisibleWidth("a\ufe0eb")).IsEqualTo(2); // VS15
        await Assert.That(WidthTable.VisibleWidth("a\ufeffb")).IsEqualTo(2); // BOM / ZWNBSP
    }

    [Test]
    public async Task EmojiZwjSequence_DoesNotInflateColumns()
    {
        // three wide runes + two zero-width joiners = 6 columns
        await Assert.That(WidthTable.VisibleWidth("👨\u200d👩\u200d👧")).IsEqualTo(6);
    }

    [Test]
    public async Task WrapToWidth_DoesNotSpendColumnsOnJoiners()
    {
        // 2+2 columns of emoji fit exactly in width 4 — the joiner must not force a wrap
        await Assert
            .That(WidthTable.WrapToWidth("😀\u200d😀", 4))
            .IsEquivalentTo(new[] { "😀\u200d😀" });
    }

    [Test]
    public async Task ScreenModel_KeepsBothEmojiOfAZwjSequenceOnOneLine()
    {
        // with the phantom column the second emoji no longer fits in 4 columns
        var vt = new VirtualTerminal(4, 1);

        new ScreenRenderer(vt).Render(new FixedLines(["😀\u200d😀"]));

        await Assert.That(AnsiTestOutput.Strip(vt.Output)).IsEqualTo("😀😀");
    }

    private sealed class FixedLines(string[] lines) : IComponent
    {
        public string[] Render(int width) => lines;

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }
}
