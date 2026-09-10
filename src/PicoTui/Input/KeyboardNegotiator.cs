namespace PicoTui.Input;

public enum NegotiationResult
{
    Kitty,
    ModifyOtherKeys,
    None,
}

public static class KeyboardNegotiator
{
    public const string StartupQuery = "\x1b[>7u\x1b[?u\x1b[c";
    public const string DisableSeq = "\x1b[<u\x1b[>4;0m";

    public static bool TryParseResponse(string seq, out NegotiationResult result)
    {
        result = NegotiationResult.None;
        if (seq == "\x1b[?7u")
        {
            result = NegotiationResult.Kitty;
            return true;
        }
        // DA response: ESC [ ? ... c → terminal answered; we enabled
        // modifyOtherKeys ourselves, so any DA means the path is usable
        if (
            seq.StartsWith("\x1b[?", StringComparison.Ordinal)
            && seq.EndsWith("c", StringComparison.Ordinal)
        )
        {
            result = NegotiationResult.ModifyOtherKeys;
            return true;
        }
        return false;
    }
}
