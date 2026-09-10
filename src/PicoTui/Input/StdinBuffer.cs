namespace PicoTui.Input;

public sealed class StdinBuffer
{
    private readonly List<byte> _pending = [];
    private bool _pasteMode;

    public void SetPasteMode(bool on) => _pasteMode = on;

    public void Append(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            _pending.Add(b);
    }

    public IEnumerable<string> Drain()
    {
        var result = new List<string>();
        while (_pending.Count > 0)
        {
            var first = _pending[0];
            if (first == 0x1b)
            {
                if (_pasteMode && StartsWith(0x1b, 0x5b, 0x32, 0x30, 0x30, 0x7e))
                {
                    var end = IndexOf(0x1b, 0x5b, 0x32, 0x30, 0x31, 0x7e);
                    if (end >= 0)
                    {
                        result.Add(Take(end + 6));
                        continue;
                    }
                    break; // paste body incomplete
                }

                var len = EscapeSequenceLength();
                if (len > 0)
                {
                    result.Add(Take(len));
                    continue;
                }
                // legacy meta: ESC + printable ASCII char → one unit (KeyDecoder → Alt+key)
                if (_pending.Count >= 2 && IsPrintableAscii(_pending[1]))
                {
                    result.Add(Take(2));
                    continue;
                }
                // bare ESC: emit alone (v1; 150ms disambiguation is a later refinement)
                result.Add(Take(1));
                continue;
            }

            if (first < 0x80)
            {
                result.Add(Take(1));
                continue;
            }

            var charLen = Utf8CharLength(first);
            if (_pending.Count < charLen)
                break; // incomplete multibyte
            result.Add(Encoding.UTF8.GetString(_pending.ToArray(), 0, charLen));
            _pending.RemoveRange(0, charLen);
        }
        return result;
    }

    /// <summary>
    /// ESC disambiguation: wait for more bytes after a lone ESC; if none
    /// arrive, the pending ESC is the Escape key. If bytes did arrive, drain
    /// whatever formed a complete sequence (v1 — the reader thread holds the
    /// ESC for this window; a cancellation-based early return is an
    /// integration refinement).
    /// </summary>
    public async Task<string> FlushEscAsync(TimeSpan timeout)
    {
        await Task.Delay(timeout);
        if (_pending.Count == 0)
            return "\x1b";
        var seq = Drain().ToArray();
        return seq.Length > 0 ? seq[0] : "\x1b";
    }

    private int EscapeSequenceLength()
    {
        // CSI: ESC [ ... final byte (0x40-0x7E); SS3: ESC O ... final byte
        if (_pending.Count >= 3 && _pending[1] is 0x5b or 0x4f)
        {
            for (var i = 2; i < _pending.Count; i++)
                if (_pending[i] is >= 0x40 and <= 0x7e)
                    return i + 1;
            return 0; // incomplete
        }
        return 0;
    }

    private bool StartsWith(params byte[] seq)
    {
        if (_pending.Count < seq.Length)
            return false;
        for (var i = 0; i < seq.Length; i++)
            if (_pending[i] != seq[i])
                return false;
        return true;
    }

    private int IndexOf(params byte[] seq)
    {
        for (var i = 0; i + seq.Length <= _pending.Count; i++)
        {
            var match = true;
            for (var j = 0; j < seq.Length; j++)
                if (_pending[i + j] != seq[j])
                {
                    match = false;
                    break;
                }
            if (match)
                return i;
        }
        return -1;
    }

    private static bool IsPrintableAscii(byte b) => b is >= 0x20 and <= 0x7e;

    private static int Utf8CharLength(byte first) =>
        first switch
        {
            >= 0xc0 and <= 0xdf => 2,
            >= 0xe0 and <= 0xef => 3,
            _ => 4,
        };

    private string Take(int count)
    {
        var s = Encoding.UTF8.GetString(_pending.ToArray(), 0, count);
        _pending.RemoveRange(0, count);
        return s;
    }
}
