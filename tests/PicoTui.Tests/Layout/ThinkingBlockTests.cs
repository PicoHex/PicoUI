namespace PicoTui.Tests.Layout;

public sealed class ThinkingBlockTests
{
    [Test]
    public async Task Collapsed_RendersHeaderOnly()
    {
        var tb = new ThinkingBlock();
        tb.Append("line one\nline two");
        var lines = tb.Render(40);
        await Assert.That(lines).Count().IsEqualTo(1);
        await Assert.That(lines[0].Contains("thinking")).IsTrue();
    }

    [Test]
    public async Task Expanded_RendersBody()
    {
        var tb = new ThinkingBlock();
        tb.Append("line one\nline two");
        tb.Toggle();
        var lines = tb.Render(40);
        await Assert.That(lines).Count().IsEqualTo(3); // header + 2 body lines
        await Assert.That(lines[1]).IsEqualTo("line one");
    }

    [Test]
    public async Task Collapsed_ShowsLineCount()
    {
        var tb = new ThinkingBlock();
        tb.Append("a\nb\nc");
        var line = tb.Render(40)[0];
        await Assert.That(line.Contains("3")).IsTrue();
    }
}
