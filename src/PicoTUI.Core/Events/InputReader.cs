using PicoTUI.Events;

namespace PicoTUI;

/// <summary>
/// Reads raw bytes from stdin and parses them into <see cref="Event"/> objects.
/// Supports standard VT100/ANSI escape sequences for special keys and mouse events.
/// </summary>
internal static class InputReader
{
    /// <summary>
    /// Blocks until an event is available and returns it.
    /// Should be called from a background thread or async context.
    /// </summary>
    public static Event? ReadEvent()
    {
        int b = Console.In.Read();
        if (b < 0) return null;

        char ch = (char)b;

        // Escape sequence
        if (ch == '\x1B')
        {
            return ParseEscapeSequence();
        }

        // Ctrl+letter (b1-b26 excluding \r, \t, etc.)
        if (b is > 0 and < 32 and not 13 and not 10 and not 9)
        {
            char letter = (char)('a' + b - 1);
            return new KeyEvent(Key.Char, letter, KeyModifiers.Control);
        }

        // Regular character including Enter and Tab
        return ch switch
        {
            '\r' or '\n' => new KeyEvent(Key.Enter),
            '\t' => new KeyEvent(Key.Tab),
            '\x7F' => new KeyEvent(Key.Backspace),
            _ => new KeyEvent(Key.Char, ch)
        };
    }

    private static Event ParseEscapeSequence()
    {
        // Check if anything follows ESC within a short timeout
        if (!Console.KeyAvailable)
            return new KeyEvent(Key.Escape);

        char next = (char)Console.In.Read();

        // ESC [ – CSI sequence
        if (next == '[')
            return ParseCsi();

        // ESC O – SS3 sequence (F1-F4 on some terminals)
        if (next == 'O')
            return ParseSs3();

        // ESC <letter> — Alt + letter
        if (char.IsLetter(next))
            return new KeyEvent(Key.Char, char.ToLower(next), KeyModifiers.Alt);

        return new KeyEvent(Key.Escape);
    }

    private static Event ParseCsi()
    {
        // Read parameter bytes (0x30-0x3F) then intermediate (0x20-0x2F) then final (0x40-0x7E)
        Span<char> buf = stackalloc char[32];
        int len = 0;

        while (Console.KeyAvailable && len < 31)
        {
            char c = (char)Console.In.Read();
            buf[len++] = c;
            if (c is >= '@' and <= '~') break;  // final byte
        }

        if (len == 0) return new KeyEvent(Key.Escape);

        char final = buf[len - 1];
        var param = buf[..(len - 1)].ToString();

        return final switch
        {
            'A' => new KeyEvent(Key.Up),
            'B' => new KeyEvent(Key.Down),
            'C' => new KeyEvent(Key.Right),
            'D' => new KeyEvent(Key.Left),
            'H' => new KeyEvent(Key.Home),
            'F' => new KeyEvent(Key.End),
            'Z' => new KeyEvent(Key.BackTab, modifiers: KeyModifiers.Shift),
            '~' => ParseTilde(param),
            'R' => ParseCursorPosition(param),
            'M' => ParseMouseX10(buf[..len]),  // legacy X10 mouse
            _ => new KeyEvent(Key.Escape)
        };
    }

    private static Event ParseSs3()
    {
        if (!Console.KeyAvailable) return new KeyEvent(Key.Escape);
        char c = (char)Console.In.Read();
        return c switch
        {
            'A' => new KeyEvent(Key.Up),
            'B' => new KeyEvent(Key.Down),
            'C' => new KeyEvent(Key.Right),
            'D' => new KeyEvent(Key.Left),
            'H' => new KeyEvent(Key.Home),
            'F' => new KeyEvent(Key.End),
            'P' => new KeyEvent(Key.F1),
            'Q' => new KeyEvent(Key.F2),
            'R' => new KeyEvent(Key.F3),
            'S' => new KeyEvent(Key.F4),
            _ => new KeyEvent(Key.Escape)
        };
    }

    private static Event ParseTilde(string param)
    {
        // Params like "1;2" mean code=1, modifiers=shift
        var parts = param.Split(';');
        int code = int.TryParse(parts[0], out var c) ? c : 0;
        var mods = parts.Length > 1 && int.TryParse(parts[1], out var m) ? XtermMods(m) : KeyModifiers.None;

        return code switch
        {
            1 or 7 => new KeyEvent(Key.Home, modifiers: mods),
            2 => new KeyEvent(Key.Insert, modifiers: mods),
            3 => new KeyEvent(Key.Delete, modifiers: mods),
            4 or 8 => new KeyEvent(Key.End, modifiers: mods),
            5 => new KeyEvent(Key.PageUp, modifiers: mods),
            6 => new KeyEvent(Key.PageDown, modifiers: mods),
            11 => new KeyEvent(Key.F1, modifiers: mods),
            12 => new KeyEvent(Key.F2, modifiers: mods),
            13 => new KeyEvent(Key.F3, modifiers: mods),
            14 => new KeyEvent(Key.F4, modifiers: mods),
            15 => new KeyEvent(Key.F5, modifiers: mods),
            17 => new KeyEvent(Key.F6, modifiers: mods),
            18 => new KeyEvent(Key.F7, modifiers: mods),
            19 => new KeyEvent(Key.F8, modifiers: mods),
            20 => new KeyEvent(Key.F9, modifiers: mods),
            21 => new KeyEvent(Key.F10, modifiers: mods),
            23 => new KeyEvent(Key.F11, modifiers: mods),
            24 => new KeyEvent(Key.F12, modifiers: mods),
            _ => new KeyEvent(Key.Escape)
        };
    }

    private static KeyModifiers XtermMods(int m)
    {
        // xterm modifier encoding: value = modifier_bitmask + 1
        int bits = m - 1;
        var result = KeyModifiers.None;
        if ((bits & 1) != 0) result |= KeyModifiers.Shift;
        if ((bits & 2) != 0) result |= KeyModifiers.Alt;
        if ((bits & 4) != 0) result |= KeyModifiers.Control;
        return result;
    }

    private static Event ParseCursorPosition(string param)
    {
        // ESC[row;colR — cursor position report; ignore
        return new KeyEvent(Key.Escape);
    }

    private static Event ParseMouseX10(ReadOnlySpan<char> seq)
    {
        // ESC[M Cb Cx Cy — legacy X10 mouse (3 bytes after 'M')
        if (seq.Length < 4) return new KeyEvent(Key.Escape);
        int cb = seq[1] - 32;
        int cx = seq[2] - 33;
        int cy = seq[3] - 33;

        var btn = (cb & 3) switch
        {
            0 => MouseButton.Left,
            1 => MouseButton.Middle,
            2 => MouseButton.Right,
            3 => MouseButton.None,
            _ => MouseButton.None
        };

        bool release = (cb & 3) == 3;
        var action = release ? MouseAction.Release : MouseAction.Press;
        var mods = KeyModifiers.None;
        if ((cb & 4) != 0) mods |= KeyModifiers.Shift;
        if ((cb & 8) != 0) mods |= KeyModifiers.Alt;
        if ((cb & 16) != 0) mods |= KeyModifiers.Control;

        return new MouseEvent(new Point(cx, cy), btn, action, mods);
    }
}
