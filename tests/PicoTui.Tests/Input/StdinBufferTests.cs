namespace PicoTui.Tests.Input;

public sealed class StdinBufferTests
{
    [Test]
    public async Task PlainChars_EachSequence()
    {
        var b = new StdinBuffer();
        b.Append("ab"u8);
        var seq = b.Drain().ToArray();
        await Assert.That(seq).Count().IsEqualTo(2);
        await Assert.That(seq[0]).IsEqualTo("a");
        await Assert.That(seq[1]).IsEqualTo("b");
    }

    [Test]
    public async Task Utf8SurrogatePair_OneSequence()
    {
        var b = new StdinBuffer();
        b.Append(Encoding.UTF8.GetBytes("🎉")); // surrogate pair
        var seq = b.Drain().ToArray();
        await Assert.That(seq).Count().IsEqualTo(1);
        await Assert.That(seq[0]).IsEqualTo("🎉");
    }

    [Test]
    public async Task EscapeSequence_OneUnit()
    {
        var b = new StdinBuffer();
        b.Append(Encoding.ASCII.GetBytes("\x1b[A")); // up arrow
        var seq = b.Drain().ToArray();
        await Assert.That(seq).Count().IsEqualTo(1);
        await Assert.That(seq[0]).IsEqualTo("\x1b[A");
    }

    [Test]
    public async Task EscapeThenPrintableChar_OneAltMetaUnit()
    {
        var b = new StdinBuffer();
        b.Append(Encoding.ASCII.GetBytes("\x1bx")); // alt+x
        var seq = b.Drain().ToArray();
        await Assert.That(seq).Count().IsEqualTo(1);
        await Assert.That(seq[0]).IsEqualTo("\x1bx"); // KeyDecoder legacy-meta → Alt+X
    }

    [Test]
    public async Task BareEscape_Alone_EscapeKey()
    {
        var b = new StdinBuffer();
        b.Append(Encoding.ASCII.GetBytes("\x1b"));
        var seq = b.Drain().ToArray();
        await Assert.That(seq).Count().IsEqualTo(1);
        await Assert.That(seq[0]).IsEqualTo("\x1b");
    }

    [Test]
    public async Task BracketedPaste_Rewrapped()
    {
        var b = new StdinBuffer();
        b.SetPasteMode(true);
        b.Append(Encoding.ASCII.GetBytes("\x1b[200~hello\x1b[201~"));
        var seq = b.Drain().ToArray();
        await Assert.That(seq).Count().IsEqualTo(1);
        await Assert.That(seq[0]).IsEqualTo("\x1b[200~hello\x1b[201~");
    }

    [Test]
    public async Task EscAlone_AfterTimeout_IsEscapeKey()
    {
        var b = new StdinBuffer();
        b.Append("\x1b"u8);
        var flushed = await b.FlushEscAsync(TimeSpan.FromMilliseconds(20));
        await Assert.That(flushed).IsEqualTo("\x1b");
    }
}
