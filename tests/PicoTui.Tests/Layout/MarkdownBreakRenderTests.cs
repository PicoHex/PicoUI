namespace PicoTui.Tests.Layout;

/// <summary>The renderer consumes the break nodes the parser now produces:
/// soft break → space, hard break → new line.</summary>
public sealed class MarkdownBreakRenderTests
{
    [Test]
    public async Task SoftBreak_RendersAsSpace()
    {
        var lines = new MarkdownComponent("a\nb").Render(40);

        await Assert.That(lines).IsEquivalentTo(new[] { "a b" });
    }

    [Test]
    public async Task HardBreak_RendersAsLineBreak()
    {
        var lines = new MarkdownComponent("a  \nb").Render(40);

        await Assert.That(lines).IsEquivalentTo(new[] { "a", "b" });
    }

    [Test]
    public async Task HardBreakInsideEmphasis_RendersAsLineBreak()
    {
        var lines = new MarkdownComponent("**a  \nb**").Render(40);

        await Assert.That(lines).IsEquivalentTo(new[] { "a", "b" });
    }

    [Test]
    public async Task EscapedBackslash_IsNotLostInRendering()
    {
        var lines = new MarkdownComponent("a" + "\\" + "\\" + "\n" + "b").Render(40);

        // one literal backslash + soft break rendered as a space
        await Assert.That(string.Concat(lines)).IsEqualTo("a" + "\\" + " b");
    }

    [Test]
    public async Task HardBreakInsideTableCell_DoesNotEmitControlChar()
    {
        var lines = new MarkdownComponent("| h |\n|---|\n| a  \nb |").Render(40);

        await Assert.That(lines.Any(l => l.Contains('\n'))).IsFalse();
    }
}
