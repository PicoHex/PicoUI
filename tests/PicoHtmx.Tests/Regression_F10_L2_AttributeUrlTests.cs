namespace PicoHtmx.Tests;

/// <summary>
/// F10a-2 — <c>H.A</c>/<c>H.Script</c>/<c>H.Link</c> sanitize their own URL argument, but a
/// caller who put the URL in the ATTRIBUTE BAG (<c>new { href = ... }</c>) bypassed
/// <see cref="H.Url"/> entirely: the bag value went straight through <see cref="H.E"/>,
/// which stops quote breakout but not scheme abuse. The attribute writer now routes every
/// URL-bearing attribute name through <see cref="H.Url"/>, so there is one gate for every
/// entry point that renders an attribute bag.
/// </summary>
public sealed class Regression_F10_L2_AttributeUrlTests
{
    private static readonly string[] UnsafeUrls =
    [
        "javascript:alert(1)",
        "JaVaScRiPt:alert(1)",
        "  javascript:alert(1)",
        "\tjavascript:alert(1)",
        "java\tscript:alert(1)",
        "data:text/html,<script>alert(1)</script>",
        "vbscript:msgbox(1)",
        "file:///etc/passwd",
    ];

    private static async Task AssertSanitized(string html)
    {
        await Assert.That(html).Contains("about:blank");
        await Assert.That(html.Contains("javascript", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(html.Contains("vbscript", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(html.Contains("data:text/html", StringComparison.OrdinalIgnoreCase)).IsFalse();
        await Assert.That(html.Contains("file://", StringComparison.OrdinalIgnoreCase)).IsFalse();
    }

    /// <summary>The four reachable URL attribute names, across every unsafe payload.</summary>
    [Test]
    public async Task AttributeBag_UrlBearingKeys_RejectUnsafeSchemes()
    {
        foreach (var url in UnsafeUrls)
        {
            await AssertSanitized(H.Tag("div", "x", new { href = url }));
            await AssertSanitized(H.Tag("div", "x", new { src = url }));
            await AssertSanitized(H.Tag("div", "x", new { action = url }));
            await AssertSanitized(H.Tag("div", "x", new { formaction = url }));
        }
    }

    /// <summary>Every builder that can carry an attribute bag is behind the same gate.</summary>
    [Test]
    public async Task EveryAttributeBagEntryPoint_IsGated()
    {
        const string evil = "javascript:alert(1)";

        await AssertSanitized(H.Div("x", new { href = evil }));
        await AssertSanitized(H.Span("x", new { href = evil }));
        await AssertSanitized(H.P("x", new { href = evil }));
        await AssertSanitized(H.Button("x", new { href = evil }));
        await AssertSanitized(H.TextArea("x", new { href = evil }));
        await AssertSanitized(H.Input(new { src = evil }));
        await AssertSanitized(H.A("x", "/safe", new { href = evil }));
        await AssertSanitized(H.A("x", "/safe", new { action = evil }));
        await AssertSanitized(H.Tag("form", "x", new { action = evil }));
        await AssertSanitized(H.Tag("button", "x", new { formaction = evil }));
        await AssertSanitized(H.Tag("img", null, new { src = evil }));
    }

    /// <summary>Key matching is ordinal-ignore-case: <c>HREF</c> is the same attribute to a browser.</summary>
    [Test]
    public async Task AttributeBag_KeyMatchIsCaseInsensitive()
    {
        await AssertSanitized(H.Tag("div", "x", new { HREF = "javascript:alert(1)" }));
        await AssertSanitized(H.Tag("div", "x", new { Src = "javascript:alert(1)" }));
        await AssertSanitized(H.Tag("div", "x", new { FormAction = "javascript:alert(1)" }));
    }

    /// <summary>Safe values are untouched: relative references and whitelisted schemes pass.</summary>
    [Test]
    public async Task AttributeBag_SafeValues_PassThroughUnchanged()
    {
        await Assert.That(H.Tag("div", "x", new { href = "/a/b" }))
            .IsEqualTo("<div href=\"/a/b\">x</div>");
        await Assert.That(H.Tag("div", "x", new { href = "https://example.com/a?b=c#d" }))
            .IsEqualTo("<div href=\"https://example.com/a?b=c#d\">x</div>");
        await Assert.That(H.Tag("div", "x", new { src = "app.js" }))
            .IsEqualTo("<div src=\"app.js\">x</div>");
        await Assert.That(H.Tag("form", "x", new { action = "?q=1" }))
            .IsEqualTo("<form action=\"?q=1\">x</form>");
        await Assert.That(H.Tag("div", "x", new { href = "#frag" }))
            .IsEqualTo("<div href=\"#frag\">x</div>");
        await Assert.That(H.Tag("div", "x", new { href = "mailto:a@b.c" }))
            .IsEqualTo("<div href=\"mailto:a@b.c\">x</div>");
    }

    /// <summary>The URL is validated BEFORE attribute escaping, so a query string is still
    /// entity-encoded exactly once (no double encoding of the sanitized value).</summary>
    [Test]
    public async Task AttributeBag_SanitizedValue_IsEscapedOnce()
    {
        await Assert.That(H.Tag("div", "x", new { href = "/p?a=1&b=2" }))
            .IsEqualTo("<div href=\"/p?a=1&amp;b=2\">x</div>");
    }

    /// <summary>Non-URL attributes keep their raw value (no accidental URL policy).</summary>
    [Test]
    public async Task AttributeBag_NonUrlAttributes_AreNotSanitized()
    {
        await Assert.That(H.Tag("div", "x", new { data_url = "javascript:alert(1)" }))
            .IsEqualTo("<div data-url=\"javascript:alert(1)\">x</div>");
        await Assert.That(H.Tag("div", "x", new { title = "a b" }))
            .IsEqualTo("<div title=\"a b\">x</div>");
    }

    /// <summary>Sanitizing inside the writer is idempotent with the builders that pre-sanitize.</summary>
    [Test]
    public async Task PreSanitizedBuilders_StayStable()
    {
        await Assert.That(H.Script("javascript:alert(1)")).Contains("src=\"about:blank\"");
        await Assert.That(H.Link("javascript:alert(1)")).Contains("href=\"about:blank\"");
        await Assert.That(H.A("x", "javascript:alert(1)")).Contains("href=\"about:blank\"");
        await Assert.That(H.A("x", "/ok")).Contains("href=\"/ok\"");
        await Assert.That(H.Script("/a.js")).IsEqualTo("<script src=\"/a.js\"></script>");
    }

    /// <summary>Boolean attributes in a bag are unaffected (true = present, false = absent).</summary>
    [Test]
    public async Task BooleanAttributes_StillRenderPresenceOnly()
    {
        await Assert.That(H.Tag("input", null, new { required_ = true }))
            .IsEqualTo("<input required-=\"\" />");
        await Assert.That(H.Tag("div", "x", new { disabled = false }))
            .IsEqualTo("<div>x</div>");
    }
}
