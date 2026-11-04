namespace PicoHtmx.Tests;

/// <summary>
/// Review 01-H1 — the escaping family was split in two: <c>A/P/Button/TextArea</c>
/// escaped their text argument, <c>Div/Span/Tag</c> emitted it verbatim. The same
/// input therefore rendered differently depending on which builder touched it
/// (PicoAgent's tag chip showed <c>a&amp;b</c> when it had an href and
/// <c>a&amp;b</c>-as-text when it did not). Every text argument is escaped now;
/// only <c>H.Raw</c> is verbatim.
/// </summary>
public sealed class HtmlEscapingConsistencyTests
{
    private const string Dirty = "a & b <c> \"d\"";

    /// <summary>Content between the first '&gt;' and the last '&lt;'.</summary>
    private static string TextNode(string html) =>
        html[(html.IndexOf('>') + 1)..html.LastIndexOf('<')];

    [Test]
    public async Task A_And_Span_ProduceByteIdenticalTextNode()
    {
        var viaSpan = TextNode(H.Span(Dirty));
        var viaA = TextNode(H.A(Dirty, "/x"));

        await Assert.That(viaA).IsEqualTo(viaSpan);
        await Assert.That(viaSpan).IsEqualTo("a &amp; b &lt;c&gt; &quot;d&quot;");
    }

    [Test]
    public async Task All_TextBuilders_ProduceByteIdenticalTextNode()
    {
        var expected = TextNode(H.Span(Dirty));

        await Assert.That(TextNode(H.Div(Dirty))).IsEqualTo(expected);
        await Assert.That(TextNode(H.Tag("section", Dirty))).IsEqualTo(expected);
        await Assert.That(TextNode(H.P(Dirty))).IsEqualTo(expected);
        await Assert.That(TextNode(H.Button(Dirty))).IsEqualTo(expected);
        await Assert.That(TextNode(H.A(Dirty, "/x"))).IsEqualTo(expected);
        await Assert.That(TextNode(H.TextArea(Dirty))).IsEqualTo(expected);
    }

    [Test]
    public async Task Tag_WithContent_EscapesMarkupLikeText()
    {
        await Assert.That(H.Tag("p", "a<b")).IsEqualTo("<p>a&lt;b</p>");
        await Assert.That(H.Div("<b>x</b>")).IsEqualTo("<div>&lt;b&gt;x&lt;/b&gt;</div>");
    }

    [Test]
    public async Task NavItem_EscapesLabelExactlyOnce()
    {
        // PicoAgent passed `H.E(name) + remove` into H.Span/H.A, so href-vs-no-href
        // chips diverged. NavItem now hands the RAW label to H.Tag (escaped once).
        await Assert.That(TextNode(Layout.NavItem("a&b", "/x"))).IsEqualTo("a&amp;b");
        await Assert
            .That(TextNode(Layout.NavItem("a&b", "/x", active: true)))
            .IsEqualTo("a&amp;b");
    }
}

/// <summary>
/// Review 01-H2 — <c>H.E</c> is an HTML escaper, not a URL validator: quote
/// escaping stops attribute breakout but not scheme abuse, so
/// <c>H.A(text, "javascript:alert(1)")</c> produced a clickable XSS. URLs now go
/// through <see cref="H.Url"/> (http/https/mailto/tel + relative paths only).
/// </summary>
public sealed class HtmlUrlContextTests
{
    [Test]
    public async Task A_JavascriptScheme_DegradesToAboutBlank()
    {
        var html = H.A("click", "javascript:alert(1)");

        await Assert.That(html).Contains("href=\"about:blank\"");
        await Assert.That(html).DoesNotContain("javascript:");
    }

    [Test]
    public async Task UrlSchemes_AreCaseAndWhitespaceInsensitive()
    {
        await Assert.That(H.A("x", "  JaVaScRiPt:alert(1)  ")).DoesNotContain("JaVaScRiPt");
        await Assert.That(H.A("x", "java\tscript:alert(1)")).DoesNotContain("java");
        await Assert.That(H.A("x", "vbscript:msgbox(1)")).Contains("href=\"about:blank\"");
        await Assert.That(H.A("x", "data:text/html,<script>alert(1)</script>"))
            .Contains("href=\"about:blank\"");
    }

    [Test]
    public async Task UrlSchemes_AllowedSet_PassesThrough()
    {
        await Assert.That(H.A("x", "https://example.com/a?b=1")).Contains("href=\"https://example.com/a?b=1\"");
        await Assert.That(H.A("x", "http://example.com")).Contains("href=\"http://example.com\"");
        await Assert.That(H.A("x", "mailto:a@b.c")).Contains("href=\"mailto:a@b.c\"");
        await Assert.That(H.A("x", "tel:+15551234")).Contains("href=\"tel:+15551234\"");
        await Assert.That(H.A("x", "/relative/path")).Contains("href=\"/relative/path\"");
        await Assert.That(H.A("x", "relative/path")).Contains("href=\"relative/path\"");
        await Assert.That(H.A("x", "#frag")).Contains("href=\"#frag\"");
    }

    [Test]
    public async Task QueryString_Ampersands_AreHtmlAttributeEncoded()
    {
        // "&" is legal in a URL but must be entity-encoded inside an HTML attribute.
        await Assert
            .That(H.A("x", "/path?a=1&b=2"))
            .Contains("href=\"/path?a=1&amp;b=2\"");
    }

    [Test]
    public async Task EveryUrlSink_RejectsJavascriptScheme()
    {
        await Assert.That(H.Script("javascript:alert(1)")).Contains("src=\"about:blank\"");
        await Assert.That(H.Link("javascript:alert(1)")).Contains("href=\"about:blank\"");
        await Assert
            .That(Ux.Form("javascript:alert(1)"))
            .DoesNotContain("javascript:");
        await Assert
            .That(Ux.ConfirmDialog("sure?", "javascript:alert(1)"))
            .DoesNotContain("javascript:");
        await Assert
            .That(Layout.NavItem("x", "javascript:alert(1)"))
            .DoesNotContain("javascript:");
        await Assert.That(Layout.Page("t", "b", styles: "javascript:alert(1)"))
            .DoesNotContain("javascript:");
    }
}

/// <summary>
/// Review 01-H1/H2 API surface: <see cref="H.Raw"/> is the single verbatim path,
/// <see cref="H.Url"/> the single URL sanitizer and <see cref="H.QueryEscape"/> the
/// percent-encoder callers previously had to hand-roll ("/attachments/{sid}/" +
/// Uri.EscapeDataString(name) at the call site).
/// </summary>
public sealed class HtmlRawAndUrlHelperTests
{
    [Test]
    public async Task Raw_IsEmittedVerbatim()
    {
        await Assert.That(H.Span(H.Raw("<b>x</b>"))).IsEqualTo("<span><b>x</b></span>");
        await Assert.That(H.Div(H.Raw("<b>x</b>"))).IsEqualTo("<div><b>x</b></div>");
        await Assert.That(H.Tag("section", H.Raw("<b>x</b>"))).IsEqualTo("<section><b>x</b></section>");
        await Assert.That(H.P(H.Raw("<b>x</b>"))).IsEqualTo("<p><b>x</b></p>");
    }

    [Test]
    public async Task Raw_PreservesEscapedCallSiteOutput()
    {
        // The pre-escaped-then-passed idiom still works, but only when it is
        // explicit — that is the whole point of H.Raw.
        await Assert.That(H.Span(H.Raw(H.E("a&b")))).IsEqualTo("<span>a&amp;b</span>");
    }

    [Test]
    public async Task Url_RejectsNonAllowlistedScheme()
    {
        await Assert.That(H.Url("javascript:alert(1)")).IsEqualTo(H.UnsafeUrl);
        await Assert.That(H.Url("JavaScript:alert(1)")).IsEqualTo(H.UnsafeUrl);
        await Assert.That(H.Url("data:text/html,<b>x</b>")).IsEqualTo(H.UnsafeUrl);
        await Assert.That(H.Url("vbscript:msgbox(1)")).IsEqualTo(H.UnsafeUrl);
        await Assert.That(H.Url("file:///etc/passwd")).IsEqualTo(H.UnsafeUrl);
    }

    [Test]
    public async Task Url_KeepsAllowlistedSchemesAndRelativeReferences()
    {
        await Assert.That(H.Url("https://example.com/a")).IsEqualTo("https://example.com/a");
        await Assert.That(H.Url("HTTPS://example.com")).IsEqualTo("HTTPS://example.com");
        await Assert.That(H.Url("mailto:a@b.c")).IsEqualTo("mailto:a@b.c");
        await Assert.That(H.Url("tel:+15551234")).IsEqualTo("tel:+15551234");
        await Assert.That(H.Url("/path/x?y=1#z")).IsEqualTo("/path/x?y=1#z");
        await Assert.That(H.Url("relative/x")).IsEqualTo("relative/x");
        await Assert.That(H.Url("//host/x")).IsEqualTo("//host/x");
        await Assert.That(H.Url("../up")).IsEqualTo("../up");
        await Assert.That(H.Url("?q=1")).IsEqualTo("?q=1");
        await Assert.That(H.Url("#frag")).IsEqualTo("#frag");
        await Assert.That(H.Url("")).IsEqualTo("");
        await Assert.That(H.Url(null)).IsEqualTo("");
    }

    [Test]
    public async Task Url_DoesNotHtmlEscapeButLeavesItToAttributeRenderer()
    {
        // H.Url owns scheme validation; HTML attribute escaping happens once, in
        // the attribute writer, so the value is not double-encoded.
        await Assert.That(H.Url("/path?a=1&b=2")).IsEqualTo("/path?a=1&b=2");
        await Assert
            .That(H.Div("x", new { data_url = H.Url("/p?a=1&b=2") }))
            .IsEqualTo("<div data-url=\"/p?a=1&amp;b=2\">x</div>");
    }

    [Test]
    public async Task QueryEscape_EncodesReservedCharacters()
    {
        await Assert.That(H.QueryEscape("a b")).IsEqualTo("a%20b");
        await Assert.That(H.QueryEscape("a&b=c#d?e")).IsEqualTo("a%26b%3Dc%23d%3Fe");
        await Assert.That(H.QueryEscape("a+b")).IsEqualTo("a%2Bb");
        await Assert.That(H.QueryEscape("azAZ09-_.~")).IsEqualTo("azAZ09-_.~");
        await Assert.That(H.QueryEscape("中文")).IsEqualTo("%E4%B8%AD%E6%96%87");
        await Assert.That(H.QueryEscape("")).IsEqualTo("");
        await Assert.That(H.QueryEscape(null)).IsEqualTo("");
    }

    [Test]
    public async Task QueryEscape_ComposesIntoAnEncodedHref()
    {
        var href = "/search?q=" + H.QueryEscape("a b&c");

        await Assert.That(H.A("go", href)).IsEqualTo("<a href=\"/search?q=a%20b%26c\">go</a>");
    }
}
