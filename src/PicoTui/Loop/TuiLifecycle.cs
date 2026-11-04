namespace PicoTui.Loop;

public sealed class TuiLifecycle
{
    public static void Enter(ITerminal t, Action<string> onInput, Action onResize)
    {
        t.Start(onInput, onResize);
        t.Write("\x1b[?1049h"); // alt screen
        // F9-1 (05-C1 residual): entering the alternate screen does not move the
        // cursor — most terminals keep the row the main screen left behind (and the
        // alt screen is ignored entirely by some multiplexers). Clear and home here,
        // before the first frame, so the session starts at 1;1 instead of wherever
        // the shell stopped.
        t.Write("\x1b[2J\x1b[H");
        t.Write("\x1b[?25l"); // hide cursor
        t.Write("\x1b[?2004h"); // bracketed paste
        t.Write("\x1b[?2026h"); // synchronized output on
    }

    /// <summary>
    /// Enters the TUI with the frame loop wired to the terminal (F9-2): one framed
    /// input unit from the reader thread is decoded into a <see cref="Key"/> and
    /// posted as an <see cref="EventKind.Input"/> event, and a resize invalidates the
    /// root. This is the production bridge between the byte pump and the loop —
    /// without it nothing in <c>src/</c> ever called <see cref="UiLoop.Post"/>.
    /// </summary>
    public static void Enter(ITerminal t, UiLoop loop)
    {
        ArgumentNullException.ThrowIfNull(loop);
        var bridge = new InputBridge(loop);
        Enter(t, bridge.OnUnit, () => loop.Post(new UiEvent(EventKind.Resize)));
    }

    public static async Task ExitAsync(ITerminal t)
    {
        try
        {
            t.Write("\x1b[<u"); // 1. disable Kitty
            t.Write("\x1b[>4;0m"); // 2. disable modifyOtherKeys
            t.Write("\x1b[?2004l"); // 3. disable bracketed paste
            t.Write("\x1b]9;4;0\x07"); // 4. clear progress indicator
            await t.DrainInputAsync(200, 50); // 5. drain input
            t.Write("\x1b[?2026l"); // 6. synchronized output off
            t.Write("\x1b[?1049l"); // 7. main screen
            t.Write("\x1b[?25h"); // 8. show cursor
        }
        finally
        {
            // 04-C2: restoring the terminal is not optional. Stop() must run even
            // when stdout is already gone (EPIPE) and one of the writes above threw,
            // and it must be the last word on the tty state.
            // F10b: Stop() is also the reader release — it cancels *and* closes the
            // stdin stream, so a host that never calls Dispose() (PicoAgent, the
            // sample) leaves no orphan pump behind to eat the next keystroke.
            t.Stop();
        }
    }
}
