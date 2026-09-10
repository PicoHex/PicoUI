namespace PicoTui.Loop;

public enum EventKind
{
    Input,
    Agent,
    Resize,
    Tick,
    Signal,
}

public sealed record UiEvent(
    EventKind Kind,
    string? Input = null,
    string? Agent = null,
    long Tick = 0
);
