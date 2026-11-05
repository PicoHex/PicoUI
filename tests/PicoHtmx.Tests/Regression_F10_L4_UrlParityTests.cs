using PicoMarkdown;

namespace PicoHtmx.Tests;

/// <summary>
/// F10a-4 — <c>H.Url</c> (PicoHtmx) and <c>MarkdownUrl.Sanitize</c> (PicoMarkdown) are the
/// two halves of one contract: "is this URL safe to put in an href/src?". They had drifted:
/// MarkdownUrl rejected digit-leading relative paths (<c>2024:x</c>, <c>1abc:</c>) as unknown
/// schemes, turned null/empty into <c>about:blank</c> and silently deleted internal
/// whitespace (<c>"a b"</c> → <c>"ab"</c> — a different resource), while H.Url kept them.
/// Both now run the same algorithm; this suite pins them against each other byte for byte.
/// </summary>
public sealed class Regression_F10_L4_UrlParityTests
{
    /// <summary>
    /// The shared corpus (extended by F13 with the <c>data:</c> verdicts). <c>Expected</c> is
    /// spelled out so the parity check cannot pass by both sides being equally wrong.
    /// </summary>
    public static IEnumerable<(string Input, string Expected)> SharedCorpus()
    {
        // allowed schemes and relative references survive verbatim
        yield return ("https://example.com/a?b=c#d", "https://example.com/a?b=c#d");
        yield return ("HTTP://example.com", "HTTP://example.com");
        yield return ("mailto:a@b.c", "mailto:a@b.c");
        yield return ("tel:+15551234", "tel:+15551234");
        yield return ("/a/b", "/a/b");
        yield return ("./a/b.md", "./a/b.md");
        yield return ("#frag", "#frag");
        yield return ("?q=1", "?q=1");
        yield return ("//evil", "//evil");
        yield return ("blank", "blank");

        // a colon whose prefix is not a legal scheme token is a relative path
        yield return ("2024:x", "2024:x");
        yield return ("1abc:", "1abc:");

        // disallowed schemes degrade to the shared fallback
        yield return ("javascript:alert(1)", "about:blank");
        yield return ("JaVaScRiPt:alert(1)", "about:blank");
        yield return ("java\tscript:alert(1)", "about:blank");
        yield return ("  javascript:alert(1)  ", "about:blank");
        yield return ("data:text/html,<script>", "about:blank");
        yield return ("vbscript:msgbox(1)", "about:blank");

        // F13: the safe raster-image data URIs are RENDERED, the rest of data: is not
        yield return ("data:image/png;base64,AAAA", "data:image/png;base64,AAAA");
        yield return ("data:image/gif;base64,R0lGODlh", "data:image/gif;base64,R0lGODlh");
        yield return ("data:image/svg+xml;base64,PHN2Zz4=", "about:blank");
        yield return ("data:image/png,XXX", "about:blank");

        // whitespace is preserved, control characters are not
        yield return ("a b", "a b");

        // empty / null collapse to the empty string (NOT about:blank)
        yield return ("", "");
        yield return (null!, "");
        yield return ("   ", "");
    }

    [Test]
    public async Task BothSanitizers_AgreeOnEveryCorpusCase()
    {
        foreach (var (input, _) in SharedCorpus())
        {
            var fromHtmx = H.Url(input);
            var fromMarkdown = MarkdownUrl.Sanitize(input);

            await Assert.That(fromHtmx).IsEqualTo(fromMarkdown);
        }
    }

    [Test]
    public async Task BothSanitizers_MatchTheExpectedValue()
    {
        foreach (var (input, expected) in SharedCorpus())
        {
            await Assert.That(H.Url(input)).IsEqualTo(expected);
            await Assert.That(MarkdownUrl.Sanitize(input)).IsEqualTo(expected);
        }
    }

    /// <summary>The fallback constants name the same value in both packages.</summary>
    [Test]
    public async Task FallbackValues_AreIdentical()
    {
        await Assert.That(H.UnsafeUrl).IsEqualTo(MarkdownUrl.Fallback);
    }

    /// <summary>Digit-leading relative destinations — the MarkdownUrl regression.</summary>
    [Test]
    public async Task DigitLeadingRelativePaths_AreNotTreatedAsSchemes()
    {
        await Assert.That(MarkdownUrl.Sanitize("2024:x")).IsEqualTo("2024:x");
        await Assert.That(MarkdownUrl.Sanitize("1abc:")).IsEqualTo("1abc:");
        await Assert.That(H.Url("2024:x")).IsEqualTo("2024:x");
        await Assert.That(H.Url("1abc:")).IsEqualTo("1abc:");
        // ... while a digit-leading token that IS a scheme in disguise is still judged by
        // its first character: a scheme must start with a letter, per RFC 3986.
        await Assert.That(MarkdownUrl.Sanitize("2javascript:alert(1)")).IsEqualTo("2javascript:alert(1)");
    }

    /// <summary>Internal whitespace is content, not noise: never silently deleted.</summary>
    [Test]
    public async Task InternalWhitespace_IsPreserved()
    {
        await Assert.That(MarkdownUrl.Sanitize("a b")).IsEqualTo("a b");
        await Assert.That(MarkdownUrl.Sanitize("/my docs/a b.md")).IsEqualTo("/my docs/a b.md");
        await Assert.That(H.Url("/my docs/a b.md")).IsEqualTo("/my docs/a b.md");
        // callers that need a wire-safe form use QueryEscape, not the sanitizer
        await Assert.That(H.QueryEscape("a b")).IsEqualTo("a%20b");
    }

    /// <summary>Empty and null mean "no destination", which both now spell as "".</summary>
    [Test]
    public async Task EmptyAndNull_BecomeEmptyString()
    {
        await Assert.That(MarkdownUrl.Sanitize(null)).IsEqualTo("");
        await Assert.That(MarkdownUrl.Sanitize("")).IsEqualTo("");
        await Assert.That(MarkdownUrl.Sanitize("   ")).IsEqualTo("");
        await Assert.That(H.Url(null)).IsEqualTo("");
        await Assert.That(H.Url("")).IsEqualTo("");
    }

    /// <summary>F13: the raster-image <c>data:</c> subset is the ONE non-whitelisted scheme
    /// that survives — both libraries must agree on exactly which form qualifies.</summary>
    [Test]
    public async Task SafeDataImageUris_PassInBothLibraries()
    {
        foreach (var url in new[]
        {
            "data:image/png;base64,AAAA",
            "data:image/jpeg;base64,AAAA",
            "data:image/jpg;base64,AAAA",
            "data:image/gif;base64,AAAA",
            "data:image/webp;base64,AAAA",
        })
        {
            await Assert.That(H.Url(url)).IsEqualTo(url);
            await Assert.That(MarkdownUrl.Sanitize(url)).IsEqualTo(url);
        }
    }

    /// <summary>Control characters are dropped before scheme detection — <c>java\tscript:</c>
    /// must not slip through as a "relative path".</summary>
    [Test]
    public async Task ControlCharacters_CannotSplitAScheme()
    {
        await Assert.That(MarkdownUrl.Sanitize("java\tscript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(MarkdownUrl.Sanitize("java\nscript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(MarkdownUrl.Sanitize("java\rscript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(H.Url("java\tscript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(H.Url("java\nscript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(H.Url("java\rscript:alert(1)")).IsEqualTo("about:blank");
    }

    /// <summary>Both are idempotent — re-sanitizing a sanitized value changes nothing.</summary>
    [Test]
    public async Task BothAreIdempotent()
    {
        foreach (var (input, _) in SharedCorpus())
        {
            var once = MarkdownUrl.Sanitize(input);
            await Assert.That(MarkdownUrl.Sanitize(once)).IsEqualTo(once);
            var htmxOnce = H.Url(input);
            await Assert.That(H.Url(htmxOnce)).IsEqualTo(htmxOnce);
        }
    }
}
