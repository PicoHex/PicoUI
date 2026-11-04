namespace PicoTui.Tests.Screen;

/// <summary>
/// F9-1 guard (05-C1 residual): the previous fix homed the cursor only in the
/// full-redraw branch, so the two paths that start a session still drew an
/// unpositioned frame:
/// <list type="number">
/// <item>the first frame (<c>_prev is null</c>) emitted the body with no erase
/// and no CUP;</item>
/// <item><see cref="TuiLifecycle.Enter"/> switched to the alternate screen
/// without clearing/homing, so the alt screen inherited the cursor row the main
/// screen had left behind.</item>
/// </list>
/// The diff renderer positions with absolute CUP and never repaints unchanged
/// rows, so either gap leaves the whole session permanently offset. The guards
/// parse the emitted byte order (not <c>Contains</c>) and also check that the
/// tracked previous frame really is the frame that was drawn.
/// </summary>
public sealed class Regression_F9_FirstFrameHomeTests
{
    private const string EraseDisplay = "\x1b[2J";
    private const string AltScreen = "\x1b[?1049h";

    private static string Snippet(string s)
    {
        var escaped = s.Replace("\x1b", "ESC", StringComparison.Ordinal);
        return escaped.Length > 40 ? escaped[..40] : escaped;
    }

    /// <summary>Asserts that the first erase in <paramref name="output"/> is
    /// immediately followed by a CUP home (<c>ESC[H</c> / <c>ESC[1;1H</c>).</summary>
    private static async Task AssertEraseFollowedByHome(string output, string path)
    {
        var idx = output.IndexOf(EraseDisplay, StringComparison.Ordinal);
        await Assert
            .That(idx >= 0)
            .IsTrue()
            .Because($"{path}: the frame must clear the screen first, got: {Snippet(output)}");
        if (idx < 0)
            return;
        var after = output[(idx + EraseDisplay.Length)..];
        var homed =
            after.StartsWith("\x1b[H", StringComparison.Ordinal)
            || after.StartsWith("\x1b[1;1H", StringComparison.Ordinal);
        await Assert
            .That(homed)
            .IsTrue()
            .Because($"{path}: ED(2J) must be followed by CUP home, got: {Snippet(after)}");
    }

    /// <summary>Number of chars the CUP home consumed (<c>ESC[H</c> = 3).</summary>
    private static int HomeLength(string after) =>
        after.StartsWith("\x1b[H", StringComparison.Ordinal) ? 3 : 6;

    /// <summary>Text of one screen row, without the wide-rune continuation markers.</summary>
    private static string RowText(ScreenBuffer buffer, int row)
    {
        var line = buffer.GetLine(row);
        var sb = new StringBuilder();
        foreach (var cell in line.Cells)
        {
            if (cell.Continuation)
                continue;
            sb.Append(cell.Ch);
        }
        return sb.ToString().TrimEnd();
    }

    [Test]
    public async Task FirstFrame_ClearsAndHomesBeforeTheBody()
    {
        var vt = new VirtualTerminal(6, 3);
        var sr = new ScreenRenderer(vt);

        sr.Render(new Text("a\nb\nc")); // very first frame: _prev is null

        await AssertEraseFollowedByHome(vt.Output, "first frame");

        // the body must start right after the home, on row 1 — not "somewhere
        // later in the stream" as a plain Contains() check would accept
        var idx = vt.Output.IndexOf(EraseDisplay, StringComparison.Ordinal);
        var after = vt.Output[(idx + EraseDisplay.Length)..];
        var body = after[HomeLength(after)..];
        await Assert.That(body).Contains("a");
    }

    [Test]
    public async Task AltScreenEntry_ClearsAndHomes()
    {
        var vt = new VirtualTerminal(10, 3);

        TuiLifecycle.Enter(vt, _ => { }, () => { });

        var alt = vt.Output.IndexOf(AltScreen, StringComparison.Ordinal);
        await Assert.That(alt >= 0).IsTrue().Because("Enter must switch to the alt screen");
        if (alt < 0)
            return;
        await AssertEraseFollowedByHome(
            vt.Output[(alt + AltScreen.Length)..],
            "alt-screen entry"
        );
    }

    [Test]
    public async Task AltScreenThenFirstFrame_AlsoHomes()
    {
        var vt = new VirtualTerminal(12, 4);
        var sr = new ScreenRenderer(vt);

        TuiLifecycle.Enter(vt, _ => { }, () => { });
        vt.Output = "";
        sr.Render(new Text("one\ntwo\nthree"));

        await AssertEraseFollowedByHome(vt.Output, "first frame after alt-screen");
    }

    [Test]
    public async Task TwoFrames_TrackedFrameIsTheOneOnScreen()
    {
        var vt = new VirtualTerminal(8, 3);
        var sr = new ScreenRenderer(vt);

        sr.Render(new Text("aaa\nbbb\nccc")); // frame 1 (first frame, no _prev)
        sr.Render(new Text("AAA\nbbb\nccc")); // frame 2: only row 0 changed

        var prev = sr.PreviousFrame;
        await Assert
            .That(prev is not null)
            .IsTrue()
            .Because("the renderer must track the frame it last drew");
        if (prev is null)
            return;
        await Assert.That(RowText(prev, 0)).IsEqualTo("AAA");
        await Assert.That(RowText(prev, 1)).IsEqualTo("bbb");
        await Assert.That(RowText(prev, 2)).IsEqualTo("ccc");
    }

    [Test]
    public async Task SecondFrame_IsPositionedAtTheChangedRow()
    {
        var vt = new VirtualTerminal(8, 3);
        var sr = new ScreenRenderer(vt);

        sr.Render(new Text("aaa\nbbb\nccc"));
        vt.Output = "";
        sr.Render(new Text("AAA\nbbb\nccc"));

        // row 0 changed ⇒ absolute CUP to 1;1 — the same "start at 1;1" invariant
        // the full-redraw path keeps, now also true after a positioned first frame
        const string sync = "\x1b[?2026h";
        var at = vt.Output.IndexOf(sync, StringComparison.Ordinal);
        await Assert.That(at >= 0).IsTrue().Because("frames are written inside a sync-update block");
        if (at < 0)
            return;
        await Assert
            .That(vt.Output[(at + sync.Length)..].StartsWith("\x1b[1;1H", StringComparison.Ordinal))
            .IsTrue();
        await Assert.That(vt.Output).Contains("AAA");
    }
}
