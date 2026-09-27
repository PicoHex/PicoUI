namespace PicoTui.Tests.Input;

/// <summary>
/// Characterization of the display-width wrapper introduced for Text/Markdown
/// wrapping (previously lines were hard-truncated by char count).
/// </summary>
public sealed class WidthTableWrapTests
{
    [Test]
    public async Task WrapToWidth_HardWrapsAtDisplayWidth()
    {
        await Assert
            .That(WidthTable.WrapToWidth("hello world", 6))
            .IsEquivalentTo(new[] { "hello ", "world" });
    }

    [Test]
    public async Task WrapToWidth_EmptyString_YieldsSingleEmptyLine()
    {
        await Assert.That(WidthTable.WrapToWidth("", 5)).IsEquivalentTo(new[] { "" });
    }

    [Test]
    public async Task WrapToWidth_NonPositiveWidth_YieldsNoLines()
    {
        await Assert.That(WidthTable.WrapToWidth("abc", 0).Length).IsEqualTo(0);
        await Assert.That(WidthTable.WrapToWidth("abc", -1).Length).IsEqualTo(0);
    }

    [Test]
    public async Task WrapToWidth_WideRunes_CountAsTwoColumns()
    {
        await Assert
            .That(WidthTable.WrapToWidth("中文文", 4))
            .IsEquivalentTo(new[] { "中文", "文" });
    }

    [Test]
    public async Task WrapToWidth_DoesNotSplitSurrogatePairs()
    {
        var lines = WidthTable.WrapToWidth("xxx😀yyyy", 4);

        await Assert.That(lines).IsEquivalentTo(new[] { "xxx", "😀yy", "yy" });
    }

    [Test]
    public async Task WrapToWidth_WiderThanMaxSingleRune_StaysOnOneLine()
    {
        await Assert.That(WidthTable.WrapToWidth("😀", 1)).IsEquivalentTo(new[] { "😀" });
    }

    [Test]
    public async Task TruncateToWidth_DoesNotSplitSurrogatePairs()
    {
        await Assert.That(WidthTable.TruncateToWidth("ab😀cd", 3)).IsEqualTo("ab");
    }
}
