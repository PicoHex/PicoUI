namespace PicoMermaid;

public sealed record MermaidArt(string[] Rows, int Width, IReadOnlyList<string> Warnings);
