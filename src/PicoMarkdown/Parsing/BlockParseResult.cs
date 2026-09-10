namespace PicoMarkdown.Parsing;

internal sealed record BlockParseResult(IReadOnlyList<BlockNode> Blocks, int LastStableLine);
