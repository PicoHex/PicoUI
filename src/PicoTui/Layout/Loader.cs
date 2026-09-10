namespace PicoTui.Layout;

public sealed class Loader : IComponent
{
    private static readonly string[] Frames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧"];
    private bool _active;
    private int _frame;

    public void SetActive(bool active) => _active = active;

    public void Advance() => _frame = (_frame + 1) % Frames.Length;

    public string[] Render(int width) => [_active ? Frames[_frame] : ""];

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
