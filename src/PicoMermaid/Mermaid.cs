namespace PicoMermaid;

public static class Mermaid
{
    public static MermaidArt Render(string source, int maxWidth)
    {
        var warnings = new List<string>();
        var lines = source.Split('\n');
        var diagram = lines.Length > 0 ? lines[0].Trim() : "";
        if (
            !diagram.StartsWith("flowchart", StringComparison.Ordinal)
            && !diagram.StartsWith("graph", StringComparison.Ordinal)
            && !diagram.StartsWith("sequenceDiagram", StringComparison.Ordinal)
        )
        {
            warnings.Add($"unsupported diagram type: '{diagram}'");
            return Fallback(lines, maxWidth, warnings);
        }

        var rows = diagram.StartsWith("sequenceDiagram", StringComparison.Ordinal)
            ? RenderSequence(lines, maxWidth, warnings)
            : RenderFlowchart(lines, maxWidth, warnings);
        if (rows.Length == 0 && warnings.Count > 0)
            return Fallback(lines, maxWidth, warnings);
        if (rows.Any(r => WidthTable(r) > maxWidth))
        {
            warnings.Add("diagram wider than available width");
            return Fallback(lines, maxWidth, warnings);
        }
        return new MermaidArt(
            rows,
            Math.Max(0, rows.Length == 0 ? 0 : rows.Max(WidthTable)),
            warnings
        );
    }

    private static string[] RenderFlowchart(string[] lines, int maxWidth, List<string> warnings)
    {
        var result = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("flowchart") || line.StartsWith("graph"))
                continue;
            var parts = line.Split("-->", StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var a = parts[0].Trim();
                var b = parts[1].Trim();
                var width = a.Length + b.Length + 5;
                if (width > maxWidth)
                {
                    warnings.Add("diagram wider than available width");
                    return [];
                }
                result.Add($"[{a}] ──▶ [{b}]");
            }
        }
        return [.. result];
    }

    private static string[] RenderSequence(string[] lines, int maxWidth, List<string> warnings)
    {
        var result = new List<string>();
        var participants = new List<string>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
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
                var colonIdx = rest.IndexOf(':');
                var b = (colonIdx >= 0 ? rest[..colonIdx] : rest).Trim();
                var label = colonIdx >= 0 ? rest[(colonIdx + 1)..].Trim() : "";
                if (a.Length == 0 || b.Length == 0)
                {
                    warnings.Add($"invalid message: '{line}'");
                    continue;
                }
                if (!participants.Contains(a))
                    participants.Add(a);
                if (!participants.Contains(b))
                    participants.Add(b);
                var row = label.Length > 0 ? $"{a} ──▶ {b}  {label}" : $"{a} ──▶ {b}";
                if (WidthTable(row) > maxWidth)
                {
                    warnings.Add("diagram wider than available width");
                    return [];
                }
                result.Add(row);
                continue;
            }
            warnings.Add($"unsupported sequence line: '{line}'");
        }
        if (participants.Count > 0)
        {
            var header = string.Join("   ", participants);
            if (WidthTable(header) > maxWidth)
            {
                warnings.Add("diagram wider than available width");
                return [];
            }
            result.Insert(0, header);
        }
        return [.. result];
    }

    private static MermaidArt Fallback(string[] lines, int maxWidth, List<string> warnings)
    {
        var rows = lines.Select(l => l.Length > maxWidth ? l[..maxWidth] : l).ToArray();
        return new MermaidArt(
            rows,
            Math.Max(0, rows.Length == 0 ? 0 : rows.Max(WidthTable)),
            warnings
        );
    }

    private static int WidthTable(string s) => s.Length; // v1: ASCII width; CJK refinement later
}
