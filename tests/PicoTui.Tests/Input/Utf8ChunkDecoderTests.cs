namespace PicoTui.Tests.Input;

/// <summary>
/// audit L9: the TUI stdin readers decoded each 4096-byte chunk with a fresh
/// UTF-8 decoder — a multi-byte character split across chunks was replaced by
/// U+FFFD (CJK input corrupted). The chunk decoder must carry decoder state
/// across calls.
/// </summary>
public sealed class Utf8ChunkDecoderTests
{
    [Test]
    public async Task MultiByteCharSplitAcrossChunks_DecodesCorrectly()
    {
        var decoder = new Utf8ChunkDecoder();
        var bytes = Encoding.UTF8.GetBytes("你好, 世界!");

        var result = new StringBuilder();
        // Feed one byte at a time — every possible split point.
        foreach (var b in bytes)
            result.Append(decoder.Decode([b], 0, 1));

        await Assert.That(result.ToString()).IsEqualTo("你好, 世界!");
        await Assert.That(result.ToString().Contains('\uFFFD')).IsFalse();
    }

    [Test]
    public async Task AsciiChunks_DecodeAsPlainText()
    {
        var decoder = new Utf8ChunkDecoder();
        var first = decoder.Decode(Encoding.UTF8.GetBytes("hello "), 0, 6);
        var second = decoder.Decode(Encoding.UTF8.GetBytes("world"), 0, 5);
        await Assert.That(first + second).IsEqualTo("hello world");
    }

    [Test]
    public async Task EmptyChunk_ReturnsEmpty()
    {
        var decoder = new Utf8ChunkDecoder();
        await Assert.That(decoder.Decode([], 0, 0)).IsEqualTo("");
    }
}
