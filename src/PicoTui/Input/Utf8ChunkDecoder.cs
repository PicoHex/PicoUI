namespace PicoTui.Input;

/// <summary>
/// audit L9: incremental UTF-8 decoding across arbitrary byte chunks — the
/// stdin readers receive 4096-byte blocks that can split a multi-byte
/// character; a fresh decoder per chunk replaced the split character with
/// U+FFFD. This decoder keeps the <see cref="Decoder"/> state across calls.
/// </summary>
public sealed class Utf8ChunkDecoder
{
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

    public string Decode(byte[] buffer, int offset, int count)
    {
        if (count == 0)
            return "";
        var chars = new char[_decoder.GetCharCount(buffer, offset, count, flush: false)];
        _decoder.GetChars(buffer, offset, count, chars, 0, flush: false);
        return new string(chars);
    }
}
