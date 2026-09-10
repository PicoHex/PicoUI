namespace PicoTui.Input;

public readonly record struct Key(
    char Char = '\0',
    bool Ctrl = false,
    bool Alt = false,
    bool Shift = false
)
{
    public static readonly Key Enter = new('\r');
    public static readonly Key Escape = new('\x1b');
    public static readonly Key Tab = new('\t');
    public static readonly Key Backspace = new('\b');
    public static readonly Key ArrowUp = new('\0') { IsArrowUp = true };
    public static readonly Key ArrowDown = new('\0') { IsArrowDown = true };
    public static readonly Key ArrowLeft = new('\0') { IsArrowLeft = true };
    public static readonly Key ArrowRight = new('\0') { IsArrowRight = true };
    public bool IsArrowUp { get; init; }
    public bool IsArrowDown { get; init; }
    public bool IsArrowLeft { get; init; }
    public bool IsArrowRight { get; init; }
}

public static class KeySpec
{
    public static bool Matches(Key key, string spec)
    {
        var parts = spec.Split('+');
        var name = parts[^1];
        var ctrl = parts.Contains("ctrl");
        var alt = parts.Contains("alt");
        var shift = parts.Contains("shift");

        if (key.Ctrl != ctrl || key.Alt != alt || key.Shift != shift)
            return false;

        return name switch
        {
            "enter" => key.Char == Key.Enter.Char,
            "tab" => key.Char == Key.Tab.Char,
            "esc" => key.Char == Key.Escape.Char,
            "up" => key.IsArrowUp,
            "down" => key.IsArrowDown,
            "left" => key.IsArrowLeft,
            "right" => key.IsArrowRight,
            _ => name.Length == 1 && key.Char == name[0],
        };
    }
}
