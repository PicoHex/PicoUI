namespace PicoTui.Terminal;

/// <summary>
/// audit L9: the OSC title escape (\x1b]0;TITLE\x07) wrote the raw title —
/// a user-controlled session name could inject terminal control sequences.
/// Strip control characters (including ESC and BEL) before emission; unicode
/// letters, marks, and punctuation pass through.
/// </summary>
public static class TitleSanitizer
{
    public static string Sanitize(string title)
    {
        if (string.IsNullOrEmpty(title))
            return "";
        var sb = new StringBuilder(title.Length);
        foreach (var c in title)
        {
            if (char.IsControl(c))
                continue;
            sb.Append(c);
        }
        return sb.ToString();
    }
}
