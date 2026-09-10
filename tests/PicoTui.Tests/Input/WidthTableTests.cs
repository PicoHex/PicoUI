namespace PicoTui.Tests.Input;

public sealed class WidthTableTests
{
    [Test]
    public async Task Ascii_OneColEach()
    {
        await Assert.That(WidthTable.VisibleWidth("abc")).IsEqualTo(3);
    }

    [Test]
    public async Task Cjk_TwoCols()
    {
        await Assert.That(WidthTable.VisibleWidth("中")).IsEqualTo(2);
        await Assert.That(WidthTable.VisibleWidth("中文")).IsEqualTo(4);
    }

    [Test]
    public async Task Emoji_TwoCols()
    {
        await Assert.That(WidthTable.VisibleWidth("🎉")).IsEqualTo(2);
    }

    [Test]
    public async Task Truncate_RespectsWidth()
    {
        var s = WidthTable.TruncateToWidth("中abc", 3);
        await Assert.That(WidthTable.VisibleWidth(s)).IsLessThanOrEqualTo(3);
    }
}
