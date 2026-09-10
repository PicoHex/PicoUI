namespace PicoMarkdown;

public sealed record MarkdownDocument(IReadOnlyList<BlockNode> Blocks, int LastStableLine);
