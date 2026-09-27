namespace PicoTui.Tests.Layout;

/// <summary>A paragraph must wrap to the available width; truncating it drops
/// assistant content (streaming chat renders whole messages).</summary>
public sealed class MarkdownComponentWrapTests
{
    [Test]
    public async Task LongWord_WrapsInsteadOfTruncating()
    {
        var m = new MarkdownComponent(new string('x', 12));

        var lines = m.Render(5);

        await Assert.That(string.Concat(lines)).IsEqualTo(new string('x', 12));
        await Assert.That(lines.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Paragraph_ContentIsPreservedAcrossWraps()
    {
        var m = new MarkdownComponent("alpha beta gamma");

        var lines = m.Render(6);

        await Assert.That(string.Concat(lines)).IsEqualTo("alpha beta gamma");
        await Assert.That(lines.All(l => WidthTable.VisibleWidth(l) <= 6)).IsTrue();
    }

    [Test]
    public async Task Heading_AlsoWraps()
    {
        var m = new MarkdownComponent("# " + new string('y', 10));

        var lines = m.Render(4);

        await Assert.That(string.Concat(lines)).Contains(new string('y', 10));
        await Assert.That(lines.All(l => WidthTable.VisibleWidth(l) <= 4)).IsTrue();
    }

    [Test]
    public async Task Render_NonPositiveWidth_ReturnsNoLines()
    {
        await Assert.That(new MarkdownComponent("abc").Render(0).Length).IsEqualTo(0);
        await Assert.That(new MarkdownComponent("abc").Render(-1).Length).IsEqualTo(0);
    }
}
