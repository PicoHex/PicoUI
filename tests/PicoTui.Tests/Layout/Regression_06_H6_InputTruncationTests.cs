using InputLine = PicoTui.Layout.Input;

namespace PicoTui.Tests.Layout;

/// <summary>
/// 06-H6 guard: <see cref="InputLine"/> must clip by display width at rune
/// boundaries — the old <c>text.Length - width</c> slice counted UTF-16 code
/// units, so a wide character or an emoji produced an orphan surrogate (invalid
/// output) or a line wider than the terminal.
/// </summary>
public sealed class Regression_06_H6_InputTruncationTests
{
    private static async Task AssertNoLoneSurrogates(string line)
    {
        for (var i = 0; i < line.Length; i++)
            if (char.IsHighSurrogate(line[i]))
                await Assert
                    .That(i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
                    .IsTrue()
                    .Because("orphan high surrogate in the rendered input line");
        for (var i = 0; i < line.Length; i++)
            if (char.IsLowSurrogate(line[i]))
                await Assert
                    .That(i > 0 && char.IsHighSurrogate(line[i - 1]))
                    .IsTrue()
                    .Because("orphan low surrogate in the rendered input line");
    }

    /// <summary>Builds "ab😀cd" the way the decoder delivers it: the surrogate
    /// pair arrives as two single-character sequences.</summary>
    private static InputLine WithEmojiText()
    {
        var input = new InputLine();
        foreach (var piece in new[] { "a", "b", "\ud83d", "\ude00", "c", "d" })
            input.HandleInput(piece);
        return input;
    }

    [Test]
    public async Task NarrowWindow_KeepsTheSurrogatePairIntact()
    {
        var input = WithEmojiText();

        var line = input.Render(3)[0];

        await AssertNoLoneSurrogates(line);
        await Assert.That(WidthTable.VisibleWidth(line)).IsLessThanOrEqualTo(3);
    }

    [Test]
    public async Task EmojiAtTheTail_IsNotCutInHalf()
    {
        var input = new InputLine();
        input.HandleInput("a");
        input.HandleInput("\ud83d");
        input.HandleInput("\ude00");

        foreach (var width in new[] { 1, 2, 3 })
        {
            var line = input.Render(width)[0];
            await AssertNoLoneSurrogates(line);
            await Assert.That(WidthTable.VisibleWidth(line)).IsLessThanOrEqualTo(width);
        }
    }

    [Test]
    public async Task CjkText_IsClippedByColumnsNotByCodeUnits()
    {
        var input = new InputLine();
        foreach (var piece in new[] { "中", "文", "中", "文" })
            input.HandleInput(piece);

        var line = input.Render(3)[0];

        // "中文中文" is 4 code units but 8 columns — a 3-column window may hold one wide rune
        await Assert.That(WidthTable.VisibleWidth(line)).IsLessThanOrEqualTo(3);
        await AssertNoLoneSurrogates(line);
    }

    [Test]
    public async Task CombiningMark_NeverStartsTheVisibleWindow()
    {
        var input = new InputLine();
        foreach (var piece in new[] { "a", "e", "\u0301", "b" }) // e + COMBINING ACUTE
            input.HandleInput(piece);

        var line = input.Render(2)[0];

        // the window may start at "e" (mark attached) but never on the bare mark
        await Assert.That(line.Length).IsGreaterThan(0);
        await Assert.That(WidthTable.IsCombining(line[0])).IsFalse();
        await Assert.That(WidthTable.VisibleWidth(line)).IsLessThanOrEqualTo(2);
        await AssertNoLoneSurrogates(line);
    }

    [Test]
    public async Task Backspace_OnAnEmoji_RemovesTheWholeRune()
    {
        var input = new InputLine();
        input.HandleInput("a");
        input.HandleInput("\ud83d");
        input.HandleInput("\ude00");

        input.HandleInput("\b");

        await Assert.That(input.Text).IsEqualTo("a");
        await AssertNoLoneSurrogates(input.Render(5)[0]);
    }
}
