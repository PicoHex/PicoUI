namespace PicoTui.Tests.Layout;

public sealed class TextTests
{
    [Test]
    public async Task SingleLine_NoWrap()
    {
        var t = new Text("hello");
        var lines = t.Render(10);
        await Assert.That(lines).Count().IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo("hello");
    }

    [Test]
    public async Task LongLine_Wraps()
    {
        var t = new Text("hello world");
        var lines = t.Render(6);
        await Assert.That(lines).Count().IsEqualTo(2);
        await Assert.That(lines[0]).IsEqualTo("hello ");
        await Assert.That(lines[1]).IsEqualTo("world");
    }

    [Test]
    public async Task MultiLine_Preserved()
    {
        var t = new Text("a\nb");
        var lines = t.Render(10);
        await Assert.That(lines).Count().IsEqualTo(2);
        await Assert.That(lines[1]).IsEqualTo("b");
    }

    [Test]
    public async Task Invalidate_ClearsCache()
    {
        var t = new Text("x");
        _ = t.Render(5);
        t.SetText("yy");
        var lines = t.Render(5);
        await Assert.That(lines[0]).IsEqualTo("yy");
    }
}
