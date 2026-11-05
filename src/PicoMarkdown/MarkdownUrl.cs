namespace PicoMarkdown;

/// <summary>
/// URL sanitizing helper for consumers that render <see cref="Link.Url"/> or
/// <see cref="Image.Url"/> into an <c>href</c>/<c>src</c> attribute.
/// </summary>
/// <remarks>
/// <para>PicoMarkdown stores link destinations exactly as written in the source (see
/// <see cref="AstNode"/>), including <c>javascript:</c> and <c>data:</c> destinations. A
/// renderer must pass every URL through <see cref="Sanitize"/> before emitting it.</para>
/// <para>The policy is identical to <c>PicoHtmx.H.Url</c> (same whitelist, same scheme
/// detection, same <c>data:</c> exception, same empty-input behaviour) and the two are
/// pinned together by a parity test. Keep them in lockstep — a renderer that mixes markdown
/// links with HTML helpers must not be able to reach two different verdicts for the same
/// URL.</para>
/// </remarks>
public static class MarkdownUrl
{
    /// <summary>The value returned for every URL that fails the scheme whitelist.</summary>
    public const string Fallback = "about:blank";

    /// <summary>
    /// Keeps a relative URL or a URL whose scheme is one of <c>http</c>, <c>https</c>,
    /// <c>mailto</c> or <c>tel</c> (compared case-insensitively), plus the ONE safe subset of
    /// <c>data:</c> — <c>data:image/(png|jpeg|jpg|gif|webp);base64,…</c>; everything else —
    /// <c>javascript:</c>, <c>data:text/html</c>, <c>data:image/svg+xml</c>, <c>vbscript:</c>,
    /// any other scheme — becomes <see cref="Fallback"/>.
    /// </summary>
    /// <param name="url">Raw destination taken from the AST.</param>
    /// <returns>The destination (ends trimmed, ASCII control characters removed — a browser
    /// ignores those anyway, so <c>"java&#9;script:"</c> would otherwise slip through), or
    /// <see cref="Fallback"/> when the scheme is not whitelisted. Internal SPACES are content
    /// and are preserved: <c>"a b"</c> stays <c>"a b"</c> (use <c>PicoHtmx.H.QueryEscape</c>
    /// or <see cref="Uri.EscapeDataString"/> when a wire-safe form is needed).
    /// <see langword="null"/>, empty and all-whitespace input return the EMPTY STRING — "no
    /// destination" is not an unsafe destination, and rewriting it to
    /// <see cref="Fallback"/> made an omitted link target look like a real URL.</returns>
    /// <remarks>
    /// A scheme exists only when the text before the first <c>:</c> is an RFC 3986 scheme
    /// token (first character a letter, then letters/digits/<c>+</c>/<c>-</c>/<c>.</c>) and no
    /// <c>/</c>, <c>?</c> or <c>#</c> precedes it. <c>2024:x</c> and <c>1abc:</c> are therefore
    /// relative paths — a real URL parser agrees.
    /// </remarks>
    public static string Sanitize(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return "";
        var value = StripIgnoredCharacters(url);
        if (value.Length == 0)
            return "";
        return HasForbiddenScheme(value) ? Fallback : value;
    }

    private static bool IsAllowedScheme(string scheme) =>
        scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
        || scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
        || scheme.Equals("mailto", StringComparison.OrdinalIgnoreCase)
        || scheme.Equals("tel", StringComparison.OrdinalIgnoreCase);

    private static bool HasForbiddenScheme(string value)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
            return false; // no scheme: relative path / query / fragment / //host
        var delimiter = value.AsSpan().IndexOfAny('/', '?', '#');
        if (delimiter >= 0 && delimiter < colon)
            return false; // the ':' is inside the path, not a scheme separator
        var scheme = value[..colon];
        if (!IsSchemeToken(scheme))
            return false; // not a scheme at all — the browser treats it as relative
        if (scheme.Equals(DataScheme, StringComparison.OrdinalIgnoreCase))
            return !IsAllowedDataUri(value); // the ONE scheme with a vetted safe subset
        return !IsAllowedScheme(scheme);
    }

    private const string DataScheme = "data";

    /// <summary>Raster image media types whose <c>data:</c> URIs are safe to render. SVG is
    /// deliberately absent: <c>data:image/svg+xml</c> is a DOCUMENT, not pixels — it runs
    /// <c>&lt;script&gt;</c> inside whichever document embeds it, which is script injection
    /// wearing an image label. <c>image/bmp</c>, <c>image/tiff</c>, … earn nothing on a page.</summary>
    private static readonly string[] SafeDataImageTypes =
    [
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/gif",
        "image/webp",
    ];

    /// <summary>True only for <c>data:image/(png|jpeg|jpg|gif|webp);base64,&lt;payload&gt;</c>.
    /// <para>The <c>;base64</c> marker must be the last parameter before the comma: a
    /// percent-encoded payload (<c>data:image/png,XXX</c>) or an extra parameter
    /// (<c>data:image/png;charset=utf-8;base64,…</c>) is not the form a browser image encoder
    /// produces, and each of those shapes is a way to smuggle a document past a check that
    /// only looked at the prefix.</para>
    /// <para>This is the same algorithm as <c>PicoHtmx.H.Url</c>, step for step, and a parity
    /// test pins the two together.</para></summary>
    private static bool IsAllowedDataUri(string url)
    {
        const string prefix = "data:";
        const string base64Suffix = ";base64";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;
        var comma = url.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0)
            return false;
        var header = url[prefix.Length..comma];
        if (!header.EndsWith(base64Suffix, StringComparison.OrdinalIgnoreCase))
            return false;
        var mediaType = header[..^base64Suffix.Length];
        return Array.Exists(
            SafeDataImageTypes,
            type => type.Equals(mediaType, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Reports whether <paramref name="scheme"/> is an RFC 3986 scheme token: a letter
    /// first, then letters, digits, <c>+</c>, <c>-</c> or <c>.</c>. A digit-leading run
    /// (<c>2024</c>, <c>1abc</c>) is NOT a scheme, so the value is a relative path.
    /// </summary>
    private static bool IsSchemeToken(ReadOnlySpan<char> scheme)
    {
        if (scheme.Length == 0 || !char.IsAsciiLetter(scheme[0]))
            return false;
        foreach (var c in scheme[1..])
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Drops ASCII tab/CR/LF, the remaining C0 controls and DEL, then trims the ends.
    /// Those characters are ignored by URL parsers — keeping them would let
    /// <c>"java&#9;script:"</c> pass as a "relative path" while the browser still ran the
    /// script. Spaces INSIDE the value are content and are never deleted.
    /// </summary>
    private static string StripIgnoredCharacters(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (IsIgnoredCharacter(c))
                continue;
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Tab, CR, LF, the other C0 controls and DEL — the characters a URL parser skips.</summary>
    private static bool IsIgnoredCharacter(char c) => c < 0x20 || c == '\u007f';
}
