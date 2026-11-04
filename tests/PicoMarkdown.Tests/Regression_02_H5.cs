namespace PicoMarkdown.Tests;

/// <summary>
/// 02-H5 — the AST keeps raw source strings (no AST rewrite); escaping/validation is
/// the renderer's job, and PicoMarkdown ships the two helpers it needs:
/// <c>MarkdownHtml.Encode</c> (text/attribute encoding) and <c>MarkdownUrl.Sanitize</c>
/// (scheme whitelist → <c>about:blank</c>).
/// </summary>
public sealed class Regression_02_H5
{
    [Test]
    public async Task Encode_EscapesAttributeBreakoutAndTags()
    {
        var encoded = MarkdownHtml.Encode("\"><script>");

        await Assert.That(encoded.Contains('"')).IsFalse();
        await Assert.That(encoded.Contains('<')).IsFalse();
        await Assert.That(encoded.Contains('>')).IsFalse();
        await Assert.That(encoded).IsEqualTo("&quot;&gt;&lt;script&gt;");
    }

    [Test]
    public async Task Encode_EscapesAmpersandExactlyOnce()
    {
        await Assert.That(MarkdownHtml.Encode("a & b")).IsEqualTo("a &amp; b");
        await Assert.That(MarkdownHtml.Encode("&amp;")).IsEqualTo("&amp;amp;");
        await Assert.That(MarkdownHtml.Encode("'")).IsEqualTo("&#39;");
        await Assert.That(MarkdownHtml.Encode("")).IsEqualTo("");
    }

    [Test]
    public async Task Sanitize_RejectsScriptAndDataSchemes()
    {
        await Assert.That(MarkdownUrl.Sanitize("javascript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(MarkdownUrl.Sanitize("JaVaScRiPt:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(MarkdownUrl.Sanitize("java\tscript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(MarkdownUrl.Sanitize(" javascript:alert(1)")).IsEqualTo("about:blank");
        await Assert.That(MarkdownUrl.Sanitize("vbscript:msgbox")).IsEqualTo("about:blank");
        await Assert.That(MarkdownUrl.Sanitize("data:text/html,<script>")).IsEqualTo("about:blank");
    }

    [Test]
    public async Task Sanitize_KeepsWhitelistedAndRelativeUrls()
    {
        await Assert.That(MarkdownUrl.Sanitize("https://example.com/a?b=c#d"))
            .IsEqualTo("https://example.com/a?b=c#d");
        await Assert.That(MarkdownUrl.Sanitize("HTTP://example.com")).IsEqualTo("HTTP://example.com");
        await Assert.That(MarkdownUrl.Sanitize("mailto:a@b.c")).IsEqualTo("mailto:a@b.c");
        await Assert.That(MarkdownUrl.Sanitize("/a/b")).IsEqualTo("/a/b");
        await Assert.That(MarkdownUrl.Sanitize("./a/b.md")).IsEqualTo("./a/b.md");
        await Assert.That(MarkdownUrl.Sanitize("#frag")).IsEqualTo("#frag");
        await Assert.That(MarkdownUrl.Sanitize("blank")).IsEqualTo("blank");
    }

    [Test]
    public async Task Sanitize_EmptyOrNull_StaysEmpty()
    {
        // F10a-4 (behaviour change): an absent destination returns the empty string, not
        // the about:blank fallback — "no URL" must not look like a real URL. Same verdict
        // as PicoHtmx.H.Url; the parity corpus in PicoHtmx.Tests pins them together.
        await Assert.That(MarkdownUrl.Sanitize("")).IsEqualTo("");
        await Assert.That(MarkdownUrl.Sanitize("   ")).IsEqualTo("");
        await Assert.That(MarkdownUrl.Sanitize(null)).IsEqualTo("");
    }

    [Test]
    public async Task Sanitize_DigitLeadingRelativePaths_AreNotSchemes()
    {
        // F10a-4: a scheme must start with a letter (RFC 3986); these are relative paths.
        await Assert.That(MarkdownUrl.Sanitize("2024:x")).IsEqualTo("2024:x");
        await Assert.That(MarkdownUrl.Sanitize("1abc:")).IsEqualTo("1abc:");
    }

    [Test]
    public async Task Sanitize_PreservesInternalSpaces()
    {
        // F10a-4: deleting internal whitespace silently pointed the link at another resource.
        await Assert.That(MarkdownUrl.Sanitize("a b")).IsEqualTo("a b");
        await Assert.That(MarkdownUrl.Sanitize("/my docs/a b.md")).IsEqualTo("/my docs/a b.md");
    }

    [Test]
    public async Task RawAstStrings_AreValidatedByTheHelpers_NotByTheParser()
    {
        // contract: the parser never rewrites URLs; the renderer must call Sanitize/Encode.
        var doc = Markdown.Parse("[x](javascript:alert)");
        var link = (Link)((Paragraph)doc.Blocks.Single()).Inlines.Single();

        await Assert.That(link.Url).IsEqualTo("javascript:alert"); // raw, by contract
        await Assert.That(MarkdownUrl.Sanitize(link.Url)).IsEqualTo("about:blank");
    }

    [Test]
    public async Task RawTextLeaksHtml_UnlessEncoded()
    {
        var doc = Markdown.Parse("<img src=x onerror=alert(1)>");
        var text = (Text)((Paragraph)doc.Blocks.Single()).Inlines.Single();

        await Assert.That(text.Value).IsEqualTo("<img src=x onerror=alert(1)>"); // raw
        var encoded = MarkdownHtml.Encode(text.Value);
        await Assert.That(encoded.Contains('<')).IsFalse();
        await Assert.That(encoded.Contains('"')).IsFalse();
    }
}
