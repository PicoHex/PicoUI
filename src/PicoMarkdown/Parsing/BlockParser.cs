namespace PicoMarkdown.Parsing;

internal static class BlockParser
{
    private const int MaxBlockDepth = 32;

    private sealed record ListRow(int Indent, bool Ordered, int StartNumber, string Content);

    public static BlockParseResult Parse(string source) => ParseBlocks(source, 0);

    private static BlockParseResult ParseBlocks(string source, int depth)
    {
        var blocks = new List<BlockNode>();
        if (depth > MaxBlockDepth)
        {
            // bomb guard: stop recursing; emit the remainder as literal paragraphs
            foreach (var l in source.Split('\n'))
                if (l.Trim().Length > 0)
                    blocks.Add(new Paragraph(InlineParser.Parse(l.Trim())));
            return new BlockParseResult(blocks, 0);
        }
        var lines = source.Split('\n');
        var paraLines = new List<string>();
        var listRows = new List<ListRow>();
        var inList = false;
        var lastStableLine = 0;

        string? fenceMarker = null;
        string? fenceLang = null;
        var codeLines = new List<string>();
        var fenceStart = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (fenceMarker is not null)
            {
                var trimmed = line.Trim();
                if (IsClosingFence(trimmed, fenceMarker))
                {
                    blocks.Add(
                        new FencedCode(fenceLang ?? "", string.Join('\n', codeLines))
                        {
                            StartLine = fenceStart,
                            EndLine = i,
                        }
                    );
                    fenceMarker = null;
                    fenceLang = null;
                    codeLines.Clear();
                    lastStableLine = i; // closing fence = stability point
                }
                else if (IsPartialClosingFence(trimmed, fenceMarker))
                {
                    // Partial closing fence while streaming — trim from content,
                    // don't close yet.
                }
                else
                {
                    codeLines.Add(line);
                }
                continue;
            }

            if (i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
            {
                FlushParagraph(blocks, paraLines);
                if (inList)
                {
                    blocks.Add(BuildList(listRows, depth));
                    listRows.Clear();
                    inList = false;
                }
                var tableStart = i;
                var lastTableLine = i + 1;
                var headerCells = SplitCells(lines[i]);
                var rows = new List<TableRow>
                {
                    new(
                        headerCells
                            .Select(c => new TableCell(InlineParser.Parse(c.Trim())))
                            .ToList(),
                        IsHeader: true
                    ),
                };
                i += 2;
                while (
                    i < lines.Length && lines[i].Trim().Length > 0 && !IsTableSeparator(lines[i])
                )
                {
                    var cells = SplitCells(lines[i])
                        .Select(c => new TableCell(InlineParser.Parse(c.Trim())))
                        .ToList();
                    rows.Add(new TableRow(cells, IsHeader: false));
                    lastTableLine = i;
                    i++;
                }
                blocks.Add(new Table(rows) { StartLine = tableStart, EndLine = lastTableLine });
                // Table terminates at the next block boundary (no blank line needed)
                lastStableLine = lastTableLine;
                i--;
                continue;
            }

            if (IsHr(line))
            {
                FlushParagraph(blocks, paraLines);
                if (inList)
                {
                    blocks.Add(BuildList(listRows, depth));
                    listRows.Clear();
                    inList = false;
                }
                blocks.Add(new Hr { StartLine = i, EndLine = i });
                lastStableLine = i; // hr closes on its own line
                continue;
            }

            if (
                TryParseListLine(
                    line,
                    out var indent,
                    out var ordered,
                    out var startNumber,
                    out var content
                )
            )
            {
                if (!inList)
                {
                    listRows.Clear();
                    inList = true;
                }
                listRows.Add(new ListRow(indent, ordered, startNumber, content));
                continue;
            }

            // a blank or non-list line ends the group; blank lines inside a loose
            // list are skipped (stay in the group) rather than closing it
            if (inList && line.Trim().Length != 0)
            {
                blocks.Add(BuildList(listRows, depth));
                listRows.Clear();
                inList = false;
                lastStableLine = i - 1; // the blank line that terminated the list
            }

            if (line.Trim().Length == 0)
            {
                FlushParagraph(blocks, paraLines);
                lastStableLine = i; // blank line closes the paragraph
                continue;
            }

            if (TryParseHeading(line, i, out var heading))
            {
                FlushParagraph(blocks, paraLines);
                blocks.Add(heading);
                lastStableLine = i; // heading closes on its own line
                continue;
            }

            if (TryParseFenceOpen(line, out fenceMarker, out fenceLang))
            {
                FlushParagraph(blocks, paraLines);
                fenceStart = i;
                continue; // open fence: NO stability advance
            }

            if (line.TrimStart().StartsWith(">"))
            {
                FlushParagraph(blocks, paraLines);
                var quoteLines = new List<string> { StripQuote(line) };
                var j = i + 1;
                while (j < lines.Length && lines[j].TrimStart().StartsWith(">"))
                {
                    quoteLines.Add(StripQuote(lines[j]));
                    j++;
                }
                blocks.Add(
                    new BlockQuote(ParseBlocks(string.Join('\n', quoteLines), depth + 1).Blocks)
                    {
                        StartLine = i,
                        EndLine = j - 1,
                    }
                );
                lastStableLine = j - 1;
                i = j - 1;
                continue;
            }

            paraLines.Add(line);
        }

        if (inList)
        {
            blocks.Add(BuildList(listRows, depth));
            listRows.Clear();
            inList = false;
        }

        if (fenceMarker is not null)
        {
            blocks.Add(
                new FencedCode(fenceLang ?? "", string.Join('\n', codeLines))
                {
                    StartLine = fenceStart,
                    EndLine = lines.Length - 1,
                }
            );
            // unclosed fence: NO stability advance
        }
        else
        {
            FlushParagraph(blocks, paraLines);
            // trailing paragraph without a blank line: NOT stable
        }

        // If the document ended cleanly (last line blank or empty doc), advance
        // to the final line index.
        if (fenceMarker is null && (lines.Length == 1 || lines[^1].Trim().Length == 0))
            lastStableLine = Math.Max(lastStableLine, lines.Length - 1);

        return new BlockParseResult(blocks, lastStableLine);
    }

    private static string StripQuote(string line)
    {
        var trimmed = line.TrimStart();
        var body = trimmed[1..];
        return body.StartsWith(' ') ? body[1..] : body;
    }

    private static bool IsTableSeparator(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|'))
            return false;
        var body = trimmed.Trim('|').Trim();
        if (body.Length == 0)
            return false;
        foreach (var part in body.Split('|', StringSplitOptions.TrimEntries))
        {
            var p = part.Trim('-', ':');
            if (p.Length != 0)
                return false;
        }
        return true;
    }

    private static string[] SplitCells(string line) =>
        line.Trim().Trim('|').Split('|', StringSplitOptions.TrimEntries);

    private static bool IsHr(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < 3)
            return false;
        var ch = trimmed[0];
        if (ch is not ('-' or '*' or '_'))
            return false;
        foreach (var c in trimmed)
            if (c != ch && c != ' ')
                return false;
        return true;
    }

    private static bool TryParseListLine(
        string line,
        out int indent,
        out bool ordered,
        out int startNumber,
        out string content
    )
    {
        indent = 0;
        ordered = false;
        startNumber = 0;
        content = "";
        var trimmed = line.TrimStart();
        indent = line.Length - trimmed.Length;
        if (trimmed.Length == 0)
            return false;

        // unordered: '-', '*', '+' followed by whitespace (CommonMark: "-x" is not a marker)
        if (trimmed[0] is '-' or '*' or '+')
        {
            if (trimmed.Length > 1 && char.IsWhiteSpace(trimmed[1]))
            {
                content = trimmed[2..].Trim();
                return true;
            }
            return false;
        }

        // ordered: N. or N) followed by whitespace
        var digits = 0;
        while (digits < trimmed.Length && char.IsDigit(trimmed[digits]))
            digits++;
        if (
            digits > 0
            && digits < trimmed.Length
            && trimmed[digits] is '.' or ')'
            && trimmed.Length > digits + 1
            && char.IsWhiteSpace(trimmed[digits + 1])
        )
        {
            ordered = true;
            startNumber = int.TryParse(trimmed[..digits], out var n) ? n : 1;
            content = trimmed[(digits + 2)..].Trim();
            return true;
        }
        return false;
    }

    private static List BuildList(List<ListRow> rows, int depth)
    {
        if (depth > MaxBlockDepth) // bomb guard: bail out to flat text
        {
            return new List(
                rows[0].Ordered,
                rows[0].StartNumber,
                rows.Select(r =>
                        (ListItem)new ListItem([new Paragraph(InlineParser.Parse(r.Content))])
                    )
                    .ToList()
            );
        }
        var items = new List<ListItem>();
        var i = 0;
        while (i < rows.Count)
        {
            var row = rows[i];
            var blocks = new List<BlockNode> { new Paragraph(InlineParser.Parse(row.Content)) };
            var j = i + 1;
            if (j < rows.Count && rows[j].Indent > row.Indent)
            {
                var nested = new List<ListRow>();
                while (j < rows.Count && rows[j].Indent > row.Indent)
                {
                    nested.Add(rows[j]);
                    j++;
                }
                blocks.Add(BuildList(nested, depth + 1));
            }
            items.Add(new ListItem(blocks));
            i = j;
        }
        var first = rows[0];
        return new List(first.Ordered, first.StartNumber, items);
    }

    private static bool TryParseFenceOpen(string line, out string? marker, out string? lang)
    {
        marker = null;
        lang = null;
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("```"))
        {
            var ticks = 0;
            while (ticks < trimmed.Length && trimmed[ticks] == '`')
                ticks++;
            marker = new string('`', ticks);
            lang = trimmed[ticks..].Trim();
            return true;
        }
        return false;
    }

    private static bool IsClosingFence(string trimmed, string marker) =>
        trimmed.Length >= marker.Length
        && trimmed.StartsWith(marker)
        && trimmed.Trim('`').Length == 0;

    private static bool IsPartialClosingFence(string trimmed, string marker) =>
        trimmed.StartsWith("`") && trimmed.Trim('`').Length == 0 && trimmed.Length < marker.Length;

    private static bool TryParseHeading(string line, int lineIndex, out Heading heading)
    {
        heading = null!;
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != '#')
            return false;

        var level = 0;
        while (level < trimmed.Length && trimmed[level] == '#' && level < 6)
            level++;
        if (level == 0 || level >= trimmed.Length)
            return false;
        if (trimmed[level] != ' ')
            return false;

        var text = trimmed[(level + 1)..].Trim();
        heading = new Heading(level, InlineParser.Parse(text))
        {
            StartLine = lineIndex,
            EndLine = lineIndex,
        };
        return true;
    }

    private static void FlushParagraph(List<BlockNode> blocks, List<string> paraLines)
    {
        if (paraLines.Count == 0)
            return;
        blocks.Add(new Paragraph(InlineParser.Parse(string.Join(' ', paraLines))));
        paraLines.Clear();
    }
}
