namespace PicoTui.Input;

public sealed class KeyDecoder
{
    public static Key? Decode(string seq)
    {
        if (seq.Length == 0)
            return null;

        if (seq[0] != '\x1b')
        {
            if (seq.Length == 1)
                return DecodePlain(seq[0]);
            return null;
        }

        // escape sequence
        if (seq.Length == 1)
            return Key.Escape; // bare ESC — the Escape key
        if (seq.Length == 2)
        {
            // legacy meta: ESC x
            if (seq[1] != '[' && seq[1] != 'O')
                return new Key(seq[1], Ctrl: false, Alt: true, Shift: false);
            return null;
        }

        var body = seq.AsSpan(1);
        if (body[0] == '[')
            return DecodeCsi(body[1..]);
        if (body[0] == 'O')
            return DecodeSs3(body[1]);
        return null;
    }

    private static Key DecodePlain(char c)
    {
        if (c is '\r')
            return Key.Enter;
        if (c is '\t')
            return Key.Tab;
        if (c is '\b' or '\x7f')
            return Key.Backspace;
        if (c < 0x20)
            return new Key((char)(c + 0x60), Ctrl: true, Alt: false, Shift: false);
        return new Key(c, Ctrl: false, Alt: false, Shift: false);
    }

    private static Key? DecodeCsi(ReadOnlySpan<char> p)
    {
        var final = p[^1];
        var rest = p[..^1].ToString();
        var parts = rest.Split(';', StringSplitOptions.RemoveEmptyEntries);

        if (final == 'u' && parts.Length >= 2)
        {
            // kitty: ESC [ code ; modifier u — modifier is a flag bitmask (1=shift, 2=alt, 4=ctrl)
            var code = int.Parse(parts[0]);
            var mod = int.Parse(parts[1]);
            var c = (char)code;
            return new Key(c, Ctrl: (mod & 4) != 0, Alt: (mod & 2) != 0, Shift: (mod & 1) != 0);
        }

        if (final is 'A' or 'B' or 'C' or 'D')
        {
            // CSI arrows: modifier param is 1 + flags (shift=1, alt=2, ctrl=4); absent = no modifiers
            var key = final switch
            {
                'A' => Key.ArrowUp,
                'B' => Key.ArrowDown,
                'C' => Key.ArrowRight,
                _ => Key.ArrowLeft,
            };
            if (parts.Length < 2)
                return key;
            var flags = int.Parse(parts[^1]) - 1;
            return key with
            {
                Ctrl = (flags & 4) != 0,
                Alt = (flags & 2) != 0,
                Shift = (flags & 1) != 0,
            };
        }

        return null;
    }

    private static Key? DecodeSs3(char c) =>
        c switch
        {
            'A' => Key.ArrowUp,
            'B' => Key.ArrowDown,
            'C' => Key.ArrowRight,
            'D' => Key.ArrowLeft,
            _ => null,
        };
}
