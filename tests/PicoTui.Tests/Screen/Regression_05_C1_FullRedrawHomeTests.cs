namespace PicoTui.Tests.Screen;

/// <summary>
/// 05-C1 guard: the full-redraw branch emits ED(2J) — which per ECMA-48 only
/// erases and never moves the cursor — so the frame must be homed right after
/// it. The guard parses the emitted byte/char order (index arithmetic), not
/// "contains", because the two sequences are unrelated in a substring test.
/// </summary>
public sealed class Regression_05_C1_FullRedrawHomeTests
{
    private const string EraseDisplay = "\x1b[2J";

    /// <summary>Asserts that the erase is immediately followed by a cursor home
    /// (<c>ESC[H</c> or <c>ESC[1;1H</c>) — nothing in between.</summary>
    private static async Task AssertHomedAfterErase(string output)
    {
        var idx = output.IndexOf(EraseDisplay, StringComparison.Ordinal);
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        var after = output[(idx + EraseDisplay.Length)..];
        var homed =
            after.StartsWith("\x1b[H", StringComparison.Ordinal)
            || after.StartsWith("\x1b[1;1H", StringComparison.Ordinal);
        await Assert
            .That(homed)
            .IsTrue()
            .Because(
                $"ED(2J) must be followed by CUP home, got: {(after.Length > 16 ? after[..16] : after)}"
            );
    }

    [Test]
    public async Task FullRedraw_HomesTheCursorRightAfterErasing()
    {
        var vt = new VirtualTerminal(5, 4);
        var sr = new ScreenRenderer(vt);
        sr.Render(new Text("a\nb\nc\nd"));
        vt.Output = "";

        sr.Render(new Text("A\nB\nC\nD")); // 4/4 rows changed → full-redraw branch

        await Assert.That(vt.Output.Contains(EraseDisplay)).IsTrue();
        await AssertHomedAfterErase(vt.Output);
    }

    [Test]
    public async Task FullRedraw_StartsTheFrameAtRowOne()
    {
        var vt = new VirtualTerminal(5, 4);
        var sr = new ScreenRenderer(vt);
        sr.Render(new Text("a\nb\nc\nd"));
        vt.Output = "";

        sr.Render(new Text("A\nB\nC\nD"));

        // body = …2J…H + row0 + CRLF + row1 …  ⇒ row 0 must start at the home
        var idx = vt.Output.IndexOf(EraseDisplay, StringComparison.Ordinal);
        var after = vt.Output[(idx + EraseDisplay.Length)..];
        var homeLen = after.StartsWith("\x1b[H", StringComparison.Ordinal) ? 3 : 6;
        var firstLine = after[homeLen..];
        var end = firstLine.IndexOf("\r\n", StringComparison.Ordinal);
        await Assert.That(end).IsGreaterThanOrEqualTo(0);
        await Assert.That(firstLine[..end]).Contains("A");
    }

    [Test]
    public async Task WidthChangeFullRedraw_AlsoHomes()
    {
        var vt = new VirtualTerminal(6, 3);
        var sr = new ScreenRenderer(vt);
        sr.Render(new Text("abc\ndef\nghi"));
        vt.Output = "";

        vt.Resize(4, 3); // width change → NeedsFullRedraw
        sr.Render(new Text("abc\ndef\nghi"));

        await Assert.That(vt.Output.Contains(EraseDisplay)).IsTrue();
        await AssertHomedAfterErase(vt.Output);
    }
}
