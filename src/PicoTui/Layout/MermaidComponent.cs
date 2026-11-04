namespace PicoTui.Layout;

/// <summary>
/// Renders a Mermaid diagram and — F9-4 (03-H2 residual) — shows what could not be
/// drawn. <see cref="Mermaid.Render"/> reports every line it skips in
/// <see cref="MermaidArt.Warnings"/>, but this component used to throw them away, so
/// a diagram with unsupported syntax rendered as a silently <em>wrong</em> picture.
/// The degraded mode is now visible twice: on <see cref="Warnings"/> (for a host that
/// wants to render its own banner) and as a trailing <c>⚠ …</c> row inside the
/// component's own output (the terminal path, which is where users actually look).
/// </summary>
public sealed class MermaidComponent : IComponent
{
    /// <summary>Leading glyph of the degraded-mode row (<c>U+26A0</c>).</summary>
    private const string WarningMark = "\u26a0";

    private string _source = "";
    private MermaidArt? _art;
    private IReadOnlyList<string> _warnings = [];
    private int _version;
    private int _artVersion = -1;
    private int _artWidth = -1;

    /// <summary>Number of <see cref="Mermaid.Render"/> calls (test observability for the render cache).</summary>
    internal int ArtRenderCount { get; private set; }

    public MermaidComponent(string source) => _source = source;

    /// <summary>
    /// Everything the last render could not draw, in source order: the parser's own
    /// warnings (unsupported lines, invalid edges, "wider than the viewport") plus the
    /// line shapes the subset renderer accepts but cannot draw faithfully (see
    /// <see cref="SourceDiagnostics"/>). Empty for a fully supported diagram, and
    /// empty again after <see cref="SetSource"/> fixes the source — the list is
    /// recomputed with the art, never appended to.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    public void SetSource(string source)
    {
        _source = source;
        _version++;
        Invalidate();
    }

    /// <summary>Memoized art (source version + width keyed), plus the degraded-mode row
    /// when the diagram is not fully renderable. When the diagram produces no row the
    /// source is returned one row per line; treat the result as read-only.</summary>
    public string[] Render(int width)
    {
        // no columns to draw into: nothing is renderable, and Mermaid.Render(0)
        // would only produce empty rows (same guard as Text/MarkdownComponent)
        if (width <= 0)
            return [];
        // memoized by width: art layout is the heaviest per-frame computation
        if (_art is null || _artWidth != width || _artVersion != _version)
        {
            _art = Mermaid.Render(_source, width);
            _warnings = [.. _art.Warnings, .. SourceDiagnostics(_source)];
            _artWidth = width;
            _artVersion = _version;
            ArtRenderCount++;
        }
        var rows = _art.Rows.Length > 0 ? _art.Rows : SourceRows();
        return _warnings.Count == 0 ? rows : [.. rows, WarningRow(_warnings, width)];
    }

    /// <summary>
    /// Last-resort rows: the raw source, one row per line. <c>IComponent.Render</c>
    /// returns rows, so a multi-line source must not be handed back as a single
    /// string (the renderer would paste the whole diagram onto one line). CR of a
    /// CRLF source is dropped — rows must not contain a carriage return.
    /// </summary>
    private string[] SourceRows()
    {
        var lines = _source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = lines[i].TrimEnd('\r');
        return lines;
    }

    /// <summary>
    /// The visible summary row. Kept to one row (the layout gives the component no
    /// scrollback) and clipped to the available columns, so the marker always
    /// survives: <c>⚠ 2 line(s) not rendered</c>.
    /// </summary>
    private static string WarningRow(IReadOnlyList<string> warnings, int width)
    {
        var lines = 0;
        foreach (var w in warnings)
        {
            if (
                w.StartsWith("unsupported", StringComparison.Ordinal)
                || w.StartsWith("invalid", StringComparison.Ordinal)
            )
                lines++;
        }
        var text =
            lines > 0
                ? $"{WarningMark} {lines} line(s) not rendered"
                : $"{WarningMark} {warnings.Count} warning(s) — diagram may be incomplete";
        return WidthTable.TruncateToWidth(text, width);
    }

    /// <summary>
    /// Line shapes <see cref="Mermaid.Render"/> accepts but cannot draw faithfully.
    /// It splits a row on the single <c>"--&gt;"</c> token, so a left-hand edge label
    /// (<c>A -- yes --&gt; B</c>) or a bidirectional arrow (<c>A &lt;--&gt; B</c>) becomes
    /// a node whose name contains the arrow fragment (<c>[A -- yes]</c>, <c>[A &lt;]</c>).
    /// The parser reports nothing for those rows (they do split into two parts), so the
    /// component — the only place the user can see it — flags them itself.
    /// Rows the parser already reports (no arrow, chain, single node) are left to it.
    /// </summary>
    private static IReadOnlyList<string> SourceDiagnostics(string source)
    {
        List<string>? warnings = null;
        var lines = source.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var arrow = line.IndexOf("-->", StringComparison.Ordinal);
            if (arrow < 0)
                continue; // not a plain edge: the parser warns about it
            if (line.AsSpan(arrow + 3).Contains("-->", StringComparison.Ordinal))
                continue; // chain (A --> B --> C): the parser warns about it
            var left = line[..arrow];
            if (!left.Contains("--", StringComparison.Ordinal) && !left.EndsWith('<'))
                continue;
            (warnings ??= []).Add($"unsupported edge syntax at line {i + 1}: '{Clip(line)}'");
        }
        return warnings ?? (IReadOnlyList<string>)[];
    }

    /// <summary>Short, single-line rendering of a source line for a warning message.</summary>
    private static string Clip(string s) => s.Length <= 40 ? s : s[..40] + "…";

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
