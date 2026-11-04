namespace PicoTui.Layout;

public interface IComponent
{
    string[] Render(int width);

    void HandleInput(string seq);

    void Invalidate();

    /// <summary>
    /// Height-aware render entry point (spec §6.2): the scroll view clips to
    /// <paramref name="height"/> and a stack distributes it across its items.
    /// </summary>
    /// <remarks>
    /// Default implementation falls back to <see cref="Render(int)"/> (unbounded
    /// content), so every existing implementer — inside this repository or in a
    /// consumer assembly — keeps compiling and keeps its current behaviour; a
    /// component opts in by declaring this two-argument overload. The screen
    /// renderer always calls this overload (05-C2).
    /// </remarks>
    string[] Render(int width, int height) => Render(width);
}
