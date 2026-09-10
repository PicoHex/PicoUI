namespace PicoTui.Loop;

public sealed class TuiLifecycle
{
    public static void Enter(ITerminal t, Action<string> onInput, Action onResize)
    {
        t.Start(onInput, onResize);
        t.Write("\x1b[?1049h"); // alt screen
        t.Write("\x1b[?25l"); // hide cursor
        t.Write("\x1b[?2004h"); // bracketed paste
        t.Write("\x1b[?2026h"); // synchronized output on
    }

    public static async Task ExitAsync(ITerminal t)
    {
        t.Write("\x1b[<u"); // 1. disable Kitty
        t.Write("\x1b[>4;0m"); // 2. disable modifyOtherKeys
        t.Write("\x1b[?2004l"); // 3. disable bracketed paste
        t.Write("\x1b]9;4;0\x07"); // 4. clear progress indicator
        await t.DrainInputAsync(200, 50); // 5. drain input
        t.Write("\x1b[?2026l"); // 6. synchronized output off
        t.Write("\x1b[?1049l"); // 7. main screen
        t.Write("\x1b[?25h"); // 8. show cursor
        t.Stop();
    }
}
