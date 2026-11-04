namespace PicoHtmx;

public static class H
{
    /// <summary>HTML void elements — the only tags that may self-close. A
    /// self-closed non-void tag is an HTML parse error (script swallows the rest
    /// of the document; div re-parents the following markup).</summary>
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area",
        "base",
        "br",
        "col",
        "embed",
        "hr",
        "img",
        "input",
        "link",
        "meta",
        "param",
        "source",
        "track",
        "wbr",
    };

    /// <summary>Substituted for any URL whose scheme is not allowlisted.</summary>
    public const string UnsafeUrl = "about:blank";

    /// <summary>The only URL schemes that may appear in a rendered attribute.
    /// Everything else (<c>javascript:</c>, <c>data:</c>, <c>vbscript:</c>, …) is
    /// replaced with <see cref="UnsafeUrl"/> — quote escaping alone stops attribute
    /// breakout but NOT scheme abuse.</summary>
    private static readonly HashSet<string> AllowedUrlSchemes = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "http",
        "https",
        "mailto",
        "tel",
    };

    /// <summary>Attribute names whose value is a URL and therefore must pass
    /// <see cref="Url"/> no matter how it arrived — including through an attribute
    /// bag (<c>new { href = ... }</c>), which used to reach the writer unfiltered.
    /// <c>xlink:href</c> cannot be spelled as a C# property name, but the gate keys
    /// off the RENDERED name, so it is covered if a bag ever produces one.</summary>
    private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href",
        "src",
        "action",
        "formaction",
        "xlink:href",
    };

    // ── Text escaping ─────────────────────────────────────────────────────
    // One escaper, one context: every builder below treats its content argument
    // as TEXT and escapes it. Markup must be explicit: pass H.Raw(html). This
    // keeps H.A(x) / H.Span(x) byte-identical for the same x (the old split —
    // A escaping, Span not — made the same string render two different ways).

    public static string E(string? raw) =>
        raw is null
            ? ""
            : raw.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");

    /// <summary>Escapes text content, preserving <c>null</c> (a null content is
    /// what makes a void element self-close instead of rendering <c>&lt;br&gt;&lt;/br&gt;</c>).</summary>
    private static string? Esc(string? raw) => raw is null ? null : E(raw);

    /// <summary>Marks a string as ready-made markup: <see cref="Raw"/> is the ONLY
    /// way to get content emitted verbatim. Everything else is escaped.</summary>
    /// <remarks><see langword="null"/> and <c>""</c> both mean "no content": a void element
    /// then self-closes (<c>&lt;br /&gt;</c>) and a normal element renders an empty pair
    /// (<c>&lt;div&gt;&lt;/div&gt;</c>). It never renders <c>&lt;br&gt;&lt;/br&gt;</c> — an end
    /// tag for a void element is a second start tag, so that pair was a visible extra
    /// break (and an extra <c>&lt;img&gt;</c> request).</remarks>
    public static RawHtml Raw(string? html) => new(html);

    // ── URL context ───────────────────────────────────────────────────────

    /// <summary>Sanitizes a URL for an <c>href</c>/<c>src</c>/<c>hx-*</c> attribute:
    /// relative references and the http/https/mailto/tel schemes pass through,
    /// every other scheme degrades to <see cref="UnsafeUrl"/>.
    /// <para>Tabs/newlines and other C0 controls are removed first (a browser ignores
    /// them, so <c>"java\tscript:"</c> would otherwise sneak through), then the ends are
    /// trimmed. Internal SPACES are content and are never deleted — callers that need a
    /// wire-safe form use <see cref="QueryEscape"/>.</para>
    /// <para>A scheme exists only when the text before the first <c>:</c> is an RFC 3986
    /// scheme token (letter first, then letter/digit/<c>+</c>/<c>-</c>/<c>.</c>):
    /// <c>2024:x</c> and <c>1abc:</c> are relative paths, not unknown schemes. Null and
    /// empty yield the empty string ("no destination"), never a fallback URL.</para>
    /// <para>This is the same algorithm as <c>PicoMarkdown.MarkdownUrl.Sanitize</c> — the two
    /// packages ship the identical whitelist and are pinned together by a parity test.
    /// Keep them in lockstep.</para>
    /// <para>Returns the URL NOT yet HTML-escaped: callers that interpolate it into
    /// an attribute must pass it through <see cref="E"/> (the attribute renderers
    /// in this class already do — <c>&amp;</c> in a query string becomes
    /// <c>&amp;amp;</c>, as an HTML attribute requires).</para></summary>
    public static string Url(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        var url = StripUrlNoise(raw);
        if (url.Length == 0)
            return "";
        return HasForbiddenScheme(url) ? UnsafeUrl : url;
    }

    private static bool HasForbiddenScheme(string url)
    {
        var colon = url.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
            return false; // no scheme: relative path / query / fragment / //host
        var delimiter = url.AsSpan().IndexOfAny('/', '?', '#');
        if (delimiter >= 0 && delimiter < colon)
            return false; // the ':' is inside the path, not a scheme separator
        var scheme = url[..colon];
        if (!IsSchemeToken(scheme))
            return false; // not a scheme at all — the browser treats it as relative
        return !AllowedUrlSchemes.Contains(scheme);
    }

    /// <summary>Percent-encodes one query-string key or value (RFC 3986 unreserved
    /// set kept, everything else — including space, <c>&amp;</c>, <c>#</c>, <c>?</c>
    /// and non-ASCII — becomes <c>%XX</c>). Saves callers from hand-rolling
    /// <c>Uri.EscapeDataString</c> at every call site.</summary>
    public static string QueryEscape(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";
        var bytes = Encoding.UTF8.GetBytes(raw);
        var sb = new StringBuilder(bytes.Length + 8);
        foreach (var b in bytes)
        {
            var c = (char)b;
            if (
                (c >= 'A' && c <= 'Z')
                || (c >= 'a' && c <= 'z')
                || (c >= '0' && c <= '9')
                || c is '-' or '_' or '.' or '~'
            )
                sb.Append(c);
            else
                sb.Append('%').Append(HexDigit(b >> 4)).Append(HexDigit(b & 0xF));
        }
        return sb.ToString();
    }

    private static char HexDigit(int value) => (char)(value < 10 ? '0' + value : 'A' + value - 10);

    private static string StripUrlNoise(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            // WHATWG URL parsing drops tab/LF/CR anywhere and trims C0/space.
            if (c is '\t' or '\n' or '\r')
                continue;
            if (c < 0x20 || c == 0x7f)
                continue;
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

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

    // ── Core (generic attrs) ──────────────────────────────────────────────
    // Generic attrs carry [DynamicallyAccessedMembers(PublicProperties)]: the
    // trimmer preserves the attrs type's public properties at every call site
    // (anonymous types included), so the reflective AppendAttributes below is
    // AOT-safe — a bare `object` param would let trimming strip those
    // properties and every attribute would silently vanish from the HTML.

    public static string Tag<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string name, string? content = null, T? attrs = null)
        where T : class => TagCore(name, Esc(content), attrs);

    public static string Tag<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string name, RawHtml content, T? attrs = null)
        where T : class => TagCore(name, content.Value, attrs);

    // Non-generic fallback for null / runtime-dynamic attrs. Passing a non-null
    // object-typed attrs bag FAILS LOUDLY: reflection cannot see its properties
    // under PublishAot trimming (DAM only flows through the generic parameter),
    // which would silently render empty attributes. Use the generic overloads.
    public static string Tag(string name, string? content = null, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>(name, Esc(content), attrs);
    }

    public static string Tag(string name, RawHtml content, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>(name, content.Value, attrs);
    }

    private static string TagCore<T>(string name, string? content, T? attrs)
        where T : class
    {
        var sb = new StringBuilder();
        sb.Append('<').Append(name);
        AppendAttributes(sb, attrs);
        // Null OR empty content means "no content": a void element must self-close.
        // Emitting the paired form produced <br></br> — and an end tag for a void
        // element is not ignored by HTML parsers, it is a SECOND start tag, so
        // H.Raw(null)/H.Raw("") silently rendered an extra break (and an extra
        // <img> request). Non-void elements keep the normal pair (<div></div>).
        if (string.IsNullOrEmpty(content) && VoidElements.Contains(name))
            sb.Append(" />");
        else
            sb.Append('>').Append(content).Append("</").Append(name).Append('>');
        return sb.ToString();
    }

    private static void ThrowIfObjectTypedAttrs(object? attrs)
    {
        if (attrs is not null)
        {
            throw new NotSupportedException(
                "Attribute objects must be passed through the generic overloads "
                    + "(e.g. H.Tag<T>(...)) so their properties survive trimming "
                    + "([DynamicallyAccessedMembers] only flows through the generic "
                    + "parameter). Object-typed attrs would silently render empty "
                    + "attributes under PublishAot."
            );
        }
    }

    public static string Div<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string? content = null, T? attrs = null)
        where T : class => TagCore("div", Esc(content), attrs);

    public static string Div<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(RawHtml content, T? attrs = null)
        where T : class => TagCore("div", content.Value, attrs);

    public static string Div(string? content = null, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("div", Esc(content), attrs);
    }

    public static string Div(RawHtml content, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("div", content.Value, attrs);
    }

    public static string Span<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string? content = null, T? attrs = null)
        where T : class => TagCore("span", Esc(content), attrs);

    public static string Span<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(RawHtml content, T? attrs = null)
        where T : class => TagCore("span", content.Value, attrs);

    public static string Span(string? content = null, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("span", Esc(content), attrs);
    }

    public static string Span(RawHtml content, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("span", content.Value, attrs);
    }

    public static string P<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string text, T? attrs = null)
        where T : class => TagCore("p", E(text), attrs);

    public static string P<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(RawHtml text, T? attrs = null)
        where T : class => TagCore("p", text.Value, attrs);

    public static string P(string text, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("p", E(text), attrs);
    }

    public static string P(RawHtml text, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("p", text.Value, attrs);
    }

    public static string Button<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string text, T? attrs = null)
        where T : class => TagCore("button", E(text), attrs);

    public static string Button<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(RawHtml text, T? attrs = null)
        where T : class => TagCore("button", text.Value, attrs);

    public static string Button(string text, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("button", E(text), attrs);
    }

    public static string Button(RawHtml text, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("button", text.Value, attrs);
    }

    public static string A<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string text, string href, T? attrs = null)
        where T : class => ACore(E(text), href, BuildAttrString(attrs));

    public static string A<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(RawHtml text, string href, T? attrs = null)
        where T : class => ACore(text.Value, href, BuildAttrString(attrs));

    public static string A(string text, string href, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return ACore(E(text), href, BuildAttrString(attrs));
    }

    public static string A(RawHtml text, string href, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return ACore(text.Value, href, BuildAttrString(attrs));
    }

    private static string ACore(string escapedText, string href, string attrStr) =>
        $"<a href=\"{E(Url(href))}\"{attrStr}>{escapedText}</a>";

    public static string Input<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(T? attrs = null)
        where T : class => TagCore("input", null, attrs);

    public static string Input(object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("input", null, attrs);
    }

    public static string TextArea<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(string? value = null, T? attrs = null)
        where T : class => TagCore("textarea", Esc(value), attrs);

    public static string TextArea<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T
    >(RawHtml value, T? attrs = null)
        where T : class => TagCore("textarea", value.Value, attrs);

    public static string TextArea(string? value = null, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("textarea", Esc(value), attrs);
    }

    public static string TextArea(RawHtml value, object? attrs = null)
    {
        ThrowIfObjectTypedAttrs(attrs);
        return TagCore<object>("textarea", value.Value, attrs);
    }

    public static string Script(string src) => TagCore("script", null, new { src = Url(src) });

    public static string Link(string href, string rel = "stylesheet") =>
        TagCore("link", null, new { href = Url(href), rel });

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:GetProperties",
        Justification = "Property preservation is guaranteed by the generic attrs parameter's "
            + "[DynamicallyAccessedMembers(PublicProperties)] constraint at every call site; "
            + "the non-generic fallback accepts only null or caller-preserved types."
    )]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2075:GetValue",
        Justification = "Property preservation is guaranteed by the generic attrs parameter's "
            + "[DynamicallyAccessedMembers(PublicProperties)] constraint at every call site; "
            + "the non-generic fallback accepts only null or caller-preserved types."
    )]
    private static void AppendAttributes(StringBuilder sb, object? attrs)
    {
        if (attrs is null)
            return;
        var attrType = attrs.GetType();
#pragma warning disable IL2075 // Attribute properties preserved via the generic attrs DAM constraint
        foreach (var prop in attrType.GetProperties())
#pragma warning restore IL2075
        {
            var name = prop.Name switch
            {
                "class" => "class",
                "@class" => "class",
                _ => prop.Name.Replace('_', '-'),
            };
            var raw = prop.GetValue(attrs);
            // HTML boolean attributes: present = true, absent = false.
            // Rendering false as "False" would still enable the attribute.
            if (raw is bool boolValue)
            {
                if (boolValue)
                    sb.Append(' ').Append(name).Append("=\"\"");
                continue;
            }
            var val = raw?.ToString();
            // A URL-bearing attribute is sanitized HERE, whatever the entry point:
            // a bag value (new { href = ... }) bypassed H.Url and let
            // "javascript:alert(1)" straight into the document. The builders that
            // pre-sanitize (H.A/H.Script/H.Link/Layout.NavItem) are unaffected —
            // H.Url is idempotent.
            if (val is not null && UrlAttributes.Contains(name))
                val = Url(val);
            if (val is not null)
                sb.Append(' ').Append(name).Append("=\"").Append(E(val)).Append('"');
        }
    }

    private static string BuildAttrString(object? attrs)
    {
        if (attrs is null)
            return "";
        var sb = new StringBuilder();
        AppendAttributes(sb, attrs);
        return sb.ToString();
    }
}
