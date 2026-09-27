namespace PicoTui.Layout;

public sealed class MermaidComponent : IComponent
{
    private string _source = "";
    private MermaidArt? _art;
    private int _version;
    private int _artVersion = -1;
    private int _artWidth = -1;

    /// <summary>Number of <see cref="Mermaid.Render"/> calls (test observability for the render cache).</summary>
    internal int ArtRenderCount { get; private set; }

    public MermaidComponent(string source) => _source = source;

    public void SetSource(string source)
    {
        _source = source;
        _version++;
        Invalidate();
    }

    /// <summary>Memoized art (source version + width keyed). When the diagram is
    /// empty the source lines are returned; treat the result as read-only.</summary>
    public string[] Render(int width)
    {
        // memoized by width: art layout is the heaviest per-frame computation
        if (_art is null || _artWidth != width || _artVersion != _version)
        {
            _art = Mermaid.Render(_source, width);
            _artWidth = width;
            _artVersion = _version;
            ArtRenderCount++;
        }
        return _art.Rows.Length > 0 ? _art.Rows : [_source];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
