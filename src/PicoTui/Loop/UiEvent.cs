namespace PicoTui.Loop;

public enum EventKind
{
    Input,
    Agent,
    Resize,
    Tick,
    Signal,
}

/// <summary>
/// One event for <see cref="UiLoop"/>.
/// </summary>
/// <param name="Key">
/// F9-2: the decoded key of an <see cref="EventKind.Input"/> event, when the
/// producer decoded it (see <see cref="InputBridge"/>). Null means "not decoded
/// yet": the loop falls back to <see cref="PicoTui.Input.KeyDecoder"/> for any
/// non-empty <paramref name="Input"/>, so a host that posts a bare string still
/// reaches <see cref="UiLoop.OnKey"/>.
/// </param>
public sealed record UiEvent(
    EventKind Kind,
    string? Input = null,
    string? Agent = null,
    long Tick = 0,
    Key? Key = null
);
