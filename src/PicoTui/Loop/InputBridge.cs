namespace PicoTui.Loop;

/// <summary>
/// F9-2 (04-C1 residual): the bridge that was missing between the terminal's byte
/// pump and the event loop. <see cref="PicoTui.Input.StdinPump"/> reads bytes and
/// frames them into one input unit per callback (<c>"a"</c>, <c>"\x1b[A"</c>,
/// <c>"\r"</c>); <see cref="KeyDecoder"/> turns a unit into a <see cref="Key"/>;
/// this class posts both to the loop, which dispatches them on its own thread.
/// </summary>
/// <remarks>
/// <para>Before this type existed, <c>Post(</c> had zero call sites in
/// <c>src/</c> and <c>samples/</c>: the loop could dispatch input events, but
/// nothing ever produced one, so no keystroke could reach a component. The bridge
/// is wired by <see cref="TuiLifecycle.Enter(ITerminal, UiLoop)"/>.</para>
/// <para>Threading: <see cref="OnUnit"/> is called on the reader thread and
/// <see cref="UiLoop.Post"/> hands the event to the loop through its channel, so the
/// handlers (<see cref="UiLoop.OnKey"/>, <see cref="UiLoop.OnInput"/>,
/// <see cref="UiLoop.OnEvent"/>) always run on the loop thread. Decoding happens
/// here — on the reader thread — so the DISPATCH side never parses keystrokes.</para>
/// </remarks>
public sealed class InputBridge
{
    private readonly UiLoop _loop;

    public InputBridge(UiLoop loop) => _loop = loop ?? throw new ArgumentNullException(nameof(loop));

    /// <summary>
    /// Terminal input callback: one already-framed unit per call. An empty unit is
    /// ignored (it would only wake the loop); anything else is posted with its
    /// decoded key — <see cref="KeyDecoder.Decode"/> returns null for sequences the
    /// decoder does not know, and those units still reach
    /// <see cref="UiLoop.OnInput"/> instead of being dropped.
    /// </summary>
    public void OnUnit(string unit)
    {
        if (string.IsNullOrEmpty(unit))
            return;
        _loop.Post(new UiEvent(EventKind.Input, Input: unit, Key: KeyDecoder.Decode(unit)));
    }
}
