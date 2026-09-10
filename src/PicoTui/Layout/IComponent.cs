namespace PicoTui.Layout;

public interface IComponent
{
    string[] Render(int width);
    void HandleInput(string seq);
    void Invalidate();
}
