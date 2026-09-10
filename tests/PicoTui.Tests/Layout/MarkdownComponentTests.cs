namespace PicoTui.Tests.Layout;

public sealed class MarkdownComponentTests
{
    [Test]
    public async Task Paragraph_RendersText()
    {
        var m = new MarkdownComponent("hello **world**");
        var lines = m.Render(40);
        await Assert.That(lines[0]).IsEqualTo("hello world");
    }

    [Test]
    public async Task Heading_RendersWithPrefix()
    {
        var m = new MarkdownComponent("# Title");
        var lines = m.Render(40);
        await Assert.That(lines[0].Contains("Title")).IsTrue();
    }

    [Test]
    public async Task FencedCode_PreservesLines()
    {
        var m = new MarkdownComponent("```cs\nint x;\n```");
        var lines = m.Render(40);
        await Assert.That(lines[0].Contains("int x;")).IsTrue();
    }

    [Test]
    public async Task List_RendersItems()
    {
        var m = new MarkdownComponent("- a\n- b");
        var lines = m.Render(40);
        await Assert.That(lines).Count().IsEqualTo(2);
        await Assert.That(lines[0].Contains("a")).IsTrue();
        await Assert.That(lines[1].Contains("b")).IsTrue();
    }

    [Test]
    public async Task SetText_UpdatesRender()
    {
        var m = new MarkdownComponent("one");
        m.SetText("two");
        var lines = m.Render(40);
        await Assert.That(lines[0]).IsEqualTo("two");
    }

    [Test]
    public async Task Table_RendersRows()
    {
        var m = new MarkdownComponent("| a | b |\n|---|---|\n| 1 | 2 |");
        var lines = m.Render(40);
        await Assert.That(lines.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(lines[0].Contains("a")).IsTrue();
        await Assert.That(lines[0].Contains("b")).IsTrue();
        await Assert.That(lines[2].Contains("1")).IsTrue();
    }
}
