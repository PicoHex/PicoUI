using PicoMarkdown;

namespace PicoHtmx.Tests;

/// <summary>
/// F13 — the F10a URL gate was too wide. It rewrote <em>every</em> <c>data:</c> URI to
/// <see cref="H.UnsafeUrl"/>, so PicoAgent's favicon and inline images
/// (<c>data:image/png;base64,…</c>) silently became <c>about:blank</c>: a functional
/// regression traded for a security win nobody needed, because a raster image payload is
/// not scriptable.
/// <list type="bullet">
/// <item>The gate now admits exactly <c>data:image/(png|jpeg|jpg|gif|webp);base64,&lt;payload&gt;</c>.
/// <c>data:image/svg+xml</c> (scriptable markup), <c>data:text/html</c>,
/// <c>data:application/*</c> and the non-base64 form stay blocked.</item>
/// <item><see cref="H.RawUrl"/> is the same kind of type marker as <see cref="H.Raw"/>:
/// the caller has already vetted the URL, so the attribute writer emits it verbatim
/// (still HTML-escaped — escaping is about attributes, trust is about schemes).</item>
/// <item><see cref="H.Url"/> and <see cref="MarkdownUrl.Sanitize"/> keep ONE algorithm, so
/// every case below is asserted against both, byte for byte.</item>
/// </list>
/// </summary>
public sealed class Regression_F13_DataUrlTests
{
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";
    private const string Svg = "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciLz4=";

    /// <summary>
    /// The F13 corpus: every case is either "kept byte for byte" or "degraded to the
    /// shared fallback". Nothing here is left to interpretation.
    /// </summary>
    public static IEnumerable<(string Input, string Expected)> DataUriCorpus()
    {
        // ── safe raster images in their base64 form, kept verbatim ────────────
        yield return (Png, Png);
        yield return ("data:image/png;base64,AAAA", "data:image/png;base64,AAAA");
        yield return ("data:image/jpeg;base64,/9j/4AAQSkZJRg==", "data:image/jpeg;base64,/9j/4AAQSkZJRg==");
        yield return ("data:image/jpg;base64,/9j/4AAQSkZJRg==", "data:image/jpg;base64,/9j/4AAQSkZJRg==");
        yield return ("data:image/gif;base64,R0lGODlhAQABAAAAACw=", "data:image/gif;base64,R0lGODlhAQABAAAAACw=");
        yield return ("data:image/webp;base64,UklGRiIAAABXRUJQVlA4", "data:image/webp;base64,UklGRiIAAABXRUJQVlA4");
        // scheme and media type are case-insensitive, as in every browser
        yield return ("DATA:IMAGE/PNG;BASE64,AAAA", "DATA:IMAGE/PNG;BASE64,AAAA");

        // ── every other data: URI stays blocked ──────────────────────────────
        yield return (Svg, "about:blank"); // scriptable markup — the classic bypass
        yield return ("data:image/svg+xml,<svg onload=alert(1)>", "about:blank");
        yield return ("data:text/html,<script>alert(1)</script>", "about:blank");
        yield return ("data:text/html;base64,PHNjcmlwdD4=", "about:blank");
        yield return ("data:application/javascript;base64,YWxlcnQoMSk=", "about:blank");
        yield return ("data:application/octet-stream;base64,AA==", "about:blank");
        // NOT the base64 form: a percent-encoded raster image is not admitted
        yield return ("data:image/png,XXX", "about:blank");
        yield return ("data:image/png,png", "about:blank");
        // base64 marker missing / not last / no comma at all
        yield return ("data:image/png;base64", "about:blank");
        yield return ("data:image/png;base64;charset=utf-8,AAAA", "about:blank");
        yield return ("data:image/png;charset=utf-8;base64,AAAA", "about:blank");
        // an unlisted raster type is not a raster type we vetted
        yield return ("data:image/bmp;base64,Qk0=", "about:blank");
        yield return ("data:image/png+xml;base64,AAAA", "about:blank");

        // ── the pre-existing rejections must not regress ─────────────────────
        yield return ("javascript:alert(1)", "about:blank");
        yield return ("JaVaScRiPt:alert(1)", "about:blank");
        yield return ("  javascript:alert(1)", "about:blank");
        yield return ("\tjavascript:alert(1)", "about:blank");
        yield return ("java\tscript:alert(1)", "about:blank");
        yield return ("java\u0000script:alert(1)", "about:blank");
        yield return ("vbscript:msgbox(1)", "about:blank");
        yield return ("file:///etc/passwd", "about:blank");

        // ── and the allowed references still pass ────────────────────────────
        yield return ("https://example.com/a?b=c#d", "https://example.com/a?b=c#d");
        yield return ("/a/b", "/a/b");
        yield return ("app.js", "app.js");
        yield return ("#frag", "#frag");
        yield return ("", "");
    }

    // ────────────────────────────────────── 1. the shared scheme verdict

    [Test]
    public async Task HtmxUrl_MatchesTheCorpusExpectation()
    {
        foreach (var (input, expected) in DataUriCorpus())
            await Assert.That(H.Url(input)).IsEqualTo(expected);
    }

    [Test]
    public async Task MarkdownSanitize_MatchesTheCorpusExpectation()
    {
        foreach (var (input, expected) in DataUriCorpus())
            await Assert.That(MarkdownUrl.Sanitize(input)).IsEqualTo(expected);
    }

    /// <summary>The two libraries must never reach two different verdicts.</summary>
    [Test]
    public async Task HtmxAndMarkdown_AgreeByteForByteOnEveryCase()
    {
        foreach (var (input, _) in DataUriCorpus())
        {
            var fromHtmx = H.Url(input);
            var fromMarkdown = MarkdownUrl.Sanitize(input);
            await Assert.That(fromHtmx).IsEqualTo(fromMarkdown);
        }
    }

    /// <summary>Re-sanitizing a kept data URI must not start rejecting it.</summary>
    [Test]
    public async Task DataUris_AreIdempotent()
    {
        foreach (var (input, _) in DataUriCorpus())
        {
            var once = H.Url(input);
            await Assert.That(H.Url(once)).IsEqualTo(once);
            var markdownOnce = MarkdownUrl.Sanitize(input);
            await Assert.That(MarkdownUrl.Sanitize(markdownOnce)).IsEqualTo(markdownOnce);
        }
    }

    // ────────────────────────────── 2. the gate in an attribute bag

    [Test]
    public async Task AttributeBag_SafeDataImage_RendersVerbatim()
    {
        await Assert.That(H.Tag("img", null, new { src = Png }))
            .IsEqualTo($"<img src=\"{Png}\" />");
        await Assert.That(H.Tag("a", "x", new { href = "data:image/png;base64,AAAA" }))
            .IsEqualTo("<a href=\"data:image/png;base64,AAAA\">x</a>");
        // video poster is a URL attribute too (F13: it was NOT gated at all before)
        await Assert.That(H.Tag("video", "x", new { poster = "data:image/png;base64,AAAA" }))
            .IsEqualTo("<video poster=\"data:image/png;base64,AAAA\">x</video>");
    }

    [Test]
    public async Task AttributeBag_UnsafeDataUris_DegradeToFallback()
    {
        foreach (var url in new[] { Svg, "data:text/html,<script>alert(1)</script>", "data:image/png,XXX" })
        {
            var html = H.Tag("img", null, new { src = url });
            await Assert.That(html).Contains("about:blank");
            await Assert.That(html.Contains("svg", StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(html.Contains("<script", StringComparison.OrdinalIgnoreCase)).IsFalse();
            await Assert.That(html.Contains("image/png,", StringComparison.OrdinalIgnoreCase)).IsFalse();
        }
    }

    /// <summary>The documented URL-key set — <c>poster</c> included (task F13 §3).</summary>
    [Test]
    public async Task AttributeBag_EveryDocumentedUrlKey_IsGated()
    {
        const string evil = "javascript:alert(1)";
        await Assert.That(H.Tag("a", "x", new { href = evil })).Contains("about:blank");
        await Assert.That(H.Tag("img", null, new { src = evil })).Contains("about:blank");
        await Assert.That(H.Tag("form", "x", new { action = evil })).Contains("about:blank");
        await Assert.That(H.Tag("button", "x", new { formaction = evil })).Contains("about:blank");
        await Assert.That(H.Tag("video", "x", new { poster = evil })).Contains("about:blank");
        // case-insensitive, like every other attribute name
        await Assert.That(H.Tag("video", "x", new { POSTER = evil })).Contains("about:blank");
        await Assert.That(H.Tag("video", "x", new { Poster = evil })).Contains("about:blank");
    }

    /// <summary>Non-URL attributes must not inherit the URL policy (no false positives).</summary>
    [Test]
    public async Task AttributeBag_NonUrlKeys_KeepTheirRawValue()
    {
        await Assert.That(H.Tag("div", "x", new { data_src = "javascript:alert(1)" }))
            .IsEqualTo("<div data-src=\"javascript:alert(1)\">x</div>");
        await Assert.That(H.Tag("div", "x", new { poster_url = "javascript:alert(1)" }))
            .IsEqualTo("<div poster-url=\"javascript:alert(1)\">x</div>");
    }

    // ────────────────────────────── 3. the H.RawUrl escape hatch

    /// <summary><see cref="H.RawUrl"/> is a type marker, exactly like <see cref="H.Raw"/>:
    /// a plain string cannot be mistaken for one.</summary>
    [Test]
    public async Task RawUrl_IsATypeMarkerNotAString()
    {
        var marker = H.RawUrl("/a/b");
        await Assert.That(marker).IsTypeOf<RawUrl>();
        await Assert.That(marker.Value).IsEqualTo("/a/b");
        await Assert.That(H.RawUrl(null).Value).IsEqualTo("");
    }

    [Test]
    public async Task RawUrl_InAnAttributeBag_BypassesTheWhitelist()
    {
        // the caller has already confirmed the value; the gate does not second-guess it
        await Assert.That(H.Tag("img", null, new { src = H.RawUrl(Svg) }))
            .IsEqualTo($"<img src=\"{Svg}\" />");
        await Assert.That(H.Tag("div", "x", new { href = H.RawUrl("myapp://deeplink/42") }))
            .IsEqualTo("<div href=\"myapp://deeplink/42\">x</div>");
    }

    /// <summary>RawUrl means "trusted scheme", NOT "trusted markup": the value is still
    /// attribute-escaped, so it cannot break out of the quotes.</summary>
    [Test]
    public async Task RawUrl_IsStillAttributeEscaped()
    {
        await Assert.That(H.Tag("div", "x", new { href = H.RawUrl("/p?a=1&b=2") }))
            .IsEqualTo("<div href=\"/p?a=1&amp;b=2\">x</div>");
        await Assert.That(H.Tag("div", "x", new { href = H.RawUrl("\"onmouseover=\"alert(1)") }))
            .IsEqualTo("<div href=\"&quot;onmouseover=&quot;alert(1)\">x</div>");
    }

    /// <summary>A RawUrl in a non-URL attribute is just its value.</summary>
    [Test]
    public async Task RawUrl_InANonUrlAttribute_IsEmittedAsTheString()
    {
        await Assert.That(H.Tag("div", "x", new { title = H.RawUrl("a b") }))
            .IsEqualTo("<div title=\"a b\">x</div>");
    }

    // ──────────────────── 4. builders that pre-sanitize also keep data: images

    [Test]
    public async Task PreSanitizingBuilders_KeepSafeDataImages()
    {
        await Assert.That(H.A("favicon", Png)).Contains($"href=\"{Png}\"");
        await Assert.That(H.Script(Png)).Contains($"src=\"{Png}\"");
        await Assert.That(H.Link(Png)).Contains($"href=\"{Png}\"");
        // ... and still refuse the scriptable ones
        await Assert.That(H.A("x", Svg)).Contains("href=\"about:blank\"");
        await Assert.That(H.Link(Svg)).Contains("href=\"about:blank\"");
    }
}
