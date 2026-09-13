const string SampleSource = """
    # PicoMarkdown

    AOT-first markdown parsing: *inline styles*, `code`, and [links](https://picohex.org).

    - zero dependencies
    - bounded AST
    - streaming metadata (LastStableLine)

    ```csharp
    var doc = Markdown.Parse(text);
    ```

    > Renders to a compact block outline, not HTML — consumers pick their output.
    """;

// PicoMarkdown usage sample: parse markdown to the bounded AST and print a
// block-level outline (streaming consumers use LastStableLine, not shown).
var source = args.Length > 0 ? File.ReadAllText(args[0]) : SampleSource;

var doc = Markdown.Parse(source);
Console.WriteLine($"{doc.Blocks.Count} top-level block(s), last stable line {doc.LastStableLine}");
Console.WriteLine(new string('-', 60));
foreach (var block in doc.Blocks)
    PrintBlock(block, 0);

static void PrintBlock(BlockNode block, int depth)
{
    var indent = new string(' ', depth * 2);
    switch (block)
    {
        case Heading h:
            Console.WriteLine($"{indent}H{h.Level}: {InlineText(h.Inlines)}");
            break;
        case Paragraph p:
            Console.WriteLine($"{indent}P: {InlineText(p.Inlines)}");
            break;
        case FencedCode c:
            Console.WriteLine($"{indent}CODE [{c.Lang}]: {c.Code.Split('\n')[0]}…");
            break;
        case PicoMarkdown.List l:
            Console.WriteLine(
                $"{indent}LIST{(l.Ordered ? $" ({l.StartNumber})" : "")}: {l.Items.Count} items"
            );
            foreach (var item in l.Items)
            foreach (var b in item.Blocks)
                PrintBlock(b, depth + 1);
            break;
        case BlockQuote q:
            Console.WriteLine($"{indent}QUOTE:");
            foreach (var b in q.Blocks)
                PrintBlock(b, depth + 1);
            break;
        case Table t:
            Console.WriteLine($"{indent}TABLE: {t.Rows.Count} rows");
            break;
        case Hr:
            Console.WriteLine($"{indent}HR");
            break;
        default:
            Console.WriteLine($"{indent}{block.GetType().Name}");
            break;
    }
}

static string InlineText(IReadOnlyList<InlineNode> inlines) =>
    string.Concat(
        inlines.Select(i =>
            i switch
            {
                Text t => t.Value,
                InlineCode c => $"`{c.Code}`",
                Link link => $"({link.Url})",
                _ => "[i]",
            }
        )
    );
