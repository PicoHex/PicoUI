namespace PicoTui.Tests.Screen;

public sealed class AnsiEncoderTests
{
    [Test]
    public async Task PlainLine_EmitsChars()
    {
        var b = new ScreenBuffer();
        b.Resize(1, 3);
        b.Set(0, 0, new Cell('a', default));
        b.Set(0, 1, new Cell('b', default));
        b.Set(0, 2, new Cell('c', default));
        var line = b.GetLine(0);
        var s = AnsiEncoder.EncodeLine(line, clearToEnd: true);
        await Assert.That(s.Contains("abc")).IsTrue();
        await Assert.That(s.Contains("\x1b[K")).IsTrue(); // clear-to-end
    }

    [Test]
    public async Task StyledRun_EmitsSgrOnce()
    {
        var bold = new Style { Bold = true };
        var b = new ScreenBuffer();
        b.Resize(1, 3);
        b.Set(0, 0, new Cell('a', bold));
        b.Set(0, 1, new Cell('b', bold));
        b.Set(0, 2, new Cell('c', default));
        var line = b.GetLine(0);
        var s = AnsiEncoder.EncodeLine(line, clearToEnd: false);
        await Assert.That(s.Contains("\x1b[1mab")).IsTrue(); // one SGR run for "ab"
        await Assert.That(s.Contains("\x1b[0m")).IsTrue(); // reset
    }

    [Test]
    public async Task Synchronized_WrapsBody()
    {
        var s = AnsiEncoder.WrapSynchronized("hi");
        await Assert.That(s).IsEqualTo("\x1b[?2026hhi\x1b[?2026l");
    }
}
