namespace PicoTui.Layout;

public sealed class MermaidComponent : IComponent
{
    private string _source = "";

    public MermaidComponent(string source) => _source = source;

    public void SetSource(string source)
    {
        _source = source;
        Invalidate();
    }

    public string[] Render(int width)
    {
        var art = Mermaid.Render(_source, width);
        return art.Rows.Length > 0 ? art.Rows : [_source];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
