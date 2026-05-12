namespace PicoTUI.Events;

/// <summary>Base class for all terminal input events.</summary>
public abstract class Event { }

/// <summary>Raised when the terminal window is resized.</summary>
public sealed class ResizeEvent : Event
{
    public ResizeEvent(Size size) { Size = size; }
    public Size Size { get; }
}

/// <summary>Raised when a key is pressed.</summary>
public sealed class KeyEvent : Event
{
    public KeyEvent(Key key, char? character = null, KeyModifiers modifiers = KeyModifiers.None)
    {
        Key = key;
        Character = character;
        Modifiers = modifiers;
    }

    /// <summary>The logical key that was pressed.</summary>
    public Key Key { get; }

    /// <summary>
    /// For printable characters this is the Unicode character.
    /// <see langword="null"/> for non-character keys.
    /// </summary>
    public char? Character { get; }

    /// <summary>Modifier keys that were held while pressing this key.</summary>
    public KeyModifiers Modifiers { get; }

    public bool IsCtrl(char c) => Key == Key.Char && Character == c && (Modifiers & KeyModifiers.Control) != 0;

    public override string ToString() =>
        $"Key({Key}, '{Character}', {Modifiers})";
}

/// <summary>Raised when a mouse button is pressed, released, or moved.</summary>
public sealed class MouseEvent : Event
{
    public MouseEvent(Point position, MouseButton button, MouseAction action, KeyModifiers modifiers = KeyModifiers.None)
    {
        Position = position;
        Button = button;
        Action = action;
        Modifiers = modifiers;
    }

    public Point Position { get; }
    public MouseButton Button { get; }
    public MouseAction Action { get; }
    public KeyModifiers Modifiers { get; }
}

/// <summary>Logical key identifiers.</summary>
public enum Key
{
    None,
    Char,
    Enter,
    Escape,
    Backspace,
    Delete,
    Tab,
    BackTab,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
    Insert,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
}

/// <summary>Modifier keys.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2
}

/// <summary>Mouse buttons.</summary>
public enum MouseButton
{
    None,
    Left,
    Middle,
    Right,
    WheelUp,
    WheelDown
}

/// <summary>Mouse action types.</summary>
public enum MouseAction
{
    Press,
    Release,
    Move
}
