using PicoMermaid;

const string SampleSource = """
    flowchart TD
        A[Parse] --> B{Renderable?}
        B -- yes --> C[ASCII art]
        B -- no --> D[Warnings]
    """;

// PicoMermaid usage sample: render a flowchart subset to ASCII art.
var source = args.Length > 0 ? File.ReadAllText(args[0]) : SampleSource;
var maxWidth = args.Length > 1 && int.TryParse(args[1], out var width) ? width : 80;

var art = Mermaid.Render(source, maxWidth);
foreach (var row in art.Rows)
    Console.WriteLine(row);
if (art.Warnings.Count > 0)
{
    Console.WriteLine(new string('-', 40));
    Console.WriteLine("warnings:");
    foreach (var warning in art.Warnings)
        Console.WriteLine($"  - {warning}");
}
