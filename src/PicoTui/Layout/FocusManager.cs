namespace PicoTui.Layout;

public sealed class FocusManager
{
    public IComponent? Focused { get; private set; }

    public void SetFocus(IComponent component) => Focused = component;

    public void HandleInput(string seq) => Focused?.HandleInput(seq);
}
