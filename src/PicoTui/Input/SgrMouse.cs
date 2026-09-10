namespace PicoTui.Input;

public sealed record MouseEvent(int X, int Y, int Button, bool WheelUp, bool WheelDown);

public static class SgrMouse
{
    public const string EnableSeq = "\x1b[?1000;1006h";
    public const string DisableSeq = "\x1b[?1000;1006l";

    public static bool TryParse(string seq, out MouseEvent evt)
    {
        evt = new MouseEvent(0, 0, 0, WheelUp: false, WheelDown: false);
        if (!seq.StartsWith("\x1b[<", StringComparison.Ordinal))
            return false;
        var body = seq[3..];
        var end = body.IndexOfAny(['M', 'm']);
        if (end < 0)
            return false;
        var parts = body[..end].Split(';');
        if (parts.Length < 3)
            return false;
        if (
            !int.TryParse(parts[0], out var code)
            || !int.TryParse(parts[1], out var x)
            || !int.TryParse(parts[2], out var y)
        )
            return false;
        evt = new MouseEvent(x, y, code, WheelUp: code == 64, WheelDown: code == 65);
        return true;
    }
}
