namespace PicoMermaid;

public static class Mermaid
{
    /// <summary>Fixed columns of the flowchart row template <c>"[a] ──▶ [b]"</c>.</summary>
    private const int FlowchartArrowColumns = 9;

    /// <summary>
    /// Renders a Mermaid subset to terminal rows.
    /// <para>
    /// Widths are terminal display columns (see <see cref="DisplayWidth"/>), not
    /// UTF-16 length, so wide (CJK/fullwidth/emoji) labels are measured the way
    /// the terminal rasterizes them. Caller input is sanitized first: C0/C1
    /// control characters (ESC/OSC/BEL/CR/…) are removed from <c>source</c>
    /// before parsing, so neither <see cref="MermaidArt.Rows"/> nor
    /// <see cref="MermaidArt.Warnings"/> can carry a terminal escape sequence.
    /// Anything the renderer cannot draw is reported in
    /// <see cref="MermaidArt.Warnings"/> — never dropped silently — and a diagram
    /// that produces no row at all degrades to the (width-clipped) source text.
    /// </para>
    /// </summary>
    /// <returns>Rows that are all at most <paramref name="maxWidth"/> columns wide.</returns>
    public static MermaidArt Render(string source, int maxWidth)
    {
        maxWidth = Math.Max(0, maxWidth); // callers may hand us a computed 0/negative width
        var warnings = new List<string>();
        var lines = DisplayWidth.Clean(source).Split('\n');
        var diagram = lines.Length > 0 ? lines[0].Trim() : "";
        if (
            !diagram.StartsWith("flowchart", StringComparison.Ordinal)
            && !diagram.StartsWith("graph", StringComparison.Ordinal)
            && !diagram.StartsWith("sequenceDiagram", StringComparison.Ordinal)
        )
        {
            warnings.Add($"unsupported diagram type: '{DisplayWidth.Clip(diagram)}'");
            return Fallback(lines, maxWidth, warnings);
        }

        var rows = diagram.StartsWith("sequenceDiagram", StringComparison.Ordinal)
            ? RenderSequence(lines, maxWidth, warnings)
            : RenderFlowchart(lines, maxWidth, warnings);
        // "we drew nothing" always degrades — independent of whether a warning
        // happened to be recorded on the way.
        if (rows.Length == 0)
            return Fallback(lines, maxWidth, warnings);
        if (rows.Any(r => DisplayWidth.Of(r) > maxWidth))
        {
            warnings.Add("diagram wider than available width");
            return Fallback(lines, maxWidth, warnings);
        }
        return new MermaidArt(rows, Width(rows), warnings);
    }

    private static string[] RenderFlowchart(string[] lines, int maxWidth, List<string> warnings)
    {
        var result = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (
                line.Length == 0
                || line.StartsWith("flowchart", StringComparison.Ordinal)
                || line.StartsWith("graph", StringComparison.Ordinal)
            )
                continue;
            var parts = line.Split("-->", StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                // single nodes (A[Start]), subgraph/end, ==> / -.->, chains
                // (A --> B --> C), style/classDef/note/click…: not renderable,
                // so say so instead of silently emitting an incomplete diagram.
                warnings.Add($"unsupported flowchart line {i + 1}: '{DisplayWidth.Clip(line)}'");
                continue;
            }
            var a = parts[0].Trim();
            var b = StripEdgeLabel(parts[1].Trim());
            if (a.Length == 0 || b.Length == 0)
            {
                warnings.Add($"invalid edge at line {i + 1}: '{DisplayWidth.Clip(line)}'");
                continue;
            }
            var width = DisplayWidth.Of(a) + DisplayWidth.Of(b) + FlowchartArrowColumns;
            if (width > maxWidth)
            {
                warnings.Add("diagram wider than available width");
                return [];
            }
            result.Add($"[{a}] ──▶ [{b}]");
        }
        return [.. result];
    }

    private static string[] RenderSequence(string[] lines, int maxWidth, List<string> warnings)
    {
        var result = new List<string>();
        var participants = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("sequenceDiagram", StringComparison.Ordinal))
                continue;
            if (line.StartsWith("participant ", StringComparison.Ordinal))
            {
                var name = line["participant ".Length..].Trim();
                if (name.Length > 0 && !participants.Contains(name))
                    participants.Add(name);
                continue;
            }
            var arrowIdx = line.IndexOf("->>", StringComparison.Ordinal);
            if (arrowIdx > 0)
            {
                var a = line[..arrowIdx].Trim();
                var rest = line[(arrowIdx + 3)..];
                var colonIdx = rest.IndexOf(':', StringComparison.Ordinal);
                var b = (colonIdx >= 0 ? rest[..colonIdx] : rest).Trim();
                var label = colonIdx >= 0 ? rest[(colonIdx + 1)..].Trim() : "";
                if (a.Length == 0 || b.Length == 0)
                {
                    warnings.Add($"invalid message at line {i + 1}: '{DisplayWidth.Clip(line)}'");
                    continue;
                }
                if (!participants.Contains(a))
                    participants.Add(a);
                if (!participants.Contains(b))
                    participants.Add(b);
                var row = label.Length > 0 ? $"{a} ──▶ {b}  {label}" : $"{a} ──▶ {b}";
                if (DisplayWidth.Of(row) > maxWidth)
                {
                    warnings.Add("diagram wider than available width");
                    return [];
                }
                result.Add(row);
                continue;
            }
            warnings.Add($"unsupported sequence line {i + 1}: '{DisplayWidth.Clip(line)}'");
        }
        if (participants.Count > 0)
        {
            var header = string.Join("   ", participants);
            if (DisplayWidth.Of(header) > maxWidth)
            {
                warnings.Add("diagram wider than available width");
                return [];
            }
            result.Insert(0, header);
        }
        return [.. result];
    }

    /// <summary>Drops a leading <c>|edge label|</c> so it is not printed as a node name.</summary>
    private static string StripEdgeLabel(string s)
    {
        if (!s.StartsWith('|'))
            return s;
        var end = s.IndexOf('|', 1);
        return end < 0 ? s : s[(end + 1)..].Trim();
    }

    private static MermaidArt Fallback(string[] lines, int maxWidth, List<string> warnings)
    {
        // source lines are already control-character free (Render cleans them)
        var rows = lines.Select(l => DisplayWidth.Truncate(l, maxWidth)).ToArray();
        return new MermaidArt(rows, Width(rows), warnings);
    }

    private static int Width(string[] rows) =>
        rows.Length == 0 ? 0 : rows.Max(DisplayWidth.Of);
}
