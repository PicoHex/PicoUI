namespace PicoTui.Tests.Layout;

public sealed class EditorTests
{
    private static string TypeAll(Editor e, string text)
    {
        foreach (var c in text)
            e.HandleInput(c.ToString());
        return e.Text;
    }

    [Test]
    public async Task Typing_AppendsText()
    {
        var e = new Editor();
        var t = TypeAll(e, "hello");
        await Assert.That(t).IsEqualTo("hello");
    }

    [Test]
    public async Task Backspace_RemovesChar()
    {
        var e = new Editor();
        TypeAll(e, "abc");
        e.HandleInput("\b");
        await Assert.That(e.Text).IsEqualTo("ab");
    }

    [Test]
    public async Task Enter_InsertsNewline()
    {
        var e = new Editor();
        TypeAll(e, "a");
        e.HandleInput("\r");
        e.HandleInput("b");
        await Assert.That(e.Text).IsEqualTo("a\nb");
    }

    [Test]
    public async Task Render_MultiLine_ProducesLines()
    {
        var e = new Editor();
        TypeAll(e, "ab\ncd");
        var lines = e.Render(10);
        await Assert.That(lines).Count().IsEqualTo(2);
        await Assert.That(lines[1]).IsEqualTo("cd");
    }

    [Test]
    public async Task SetFocus_EnablesInput()
    {
        var e = new Editor();
        e.SetFocus(true);
        TypeAll(e, "x");
        await Assert.That(e.Text).IsEqualTo("x");
    }
}
