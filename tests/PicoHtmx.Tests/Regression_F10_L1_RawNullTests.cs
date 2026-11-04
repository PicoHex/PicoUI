namespace PicoHtmx.Tests;

/// <summary>
/// F10a-1 — <c>H.Raw(null)</c> carries no content, but it still occupied the
/// "content" slot of the element, so a void element took the paired-tag branch and
/// rendered <c>&lt;br&gt;&lt;/br&gt;</c>. Browsers parse an end tag for a void element as a
/// SECOND start tag (<c>&lt;/br&gt;</c> ≡ <c>&lt;br&gt;</c>), so one null Raw was a visible
/// extra break — and for <c>&lt;img&gt;</c> an extra request. Null/empty content must make a
/// void element self-close and must never emit an end tag.
/// </summary>
public sealed class Regression_F10_L1_RawNullTests
{
    /// <summary>The null Raw still exposes the empty string as its value (no API change).</summary>
    [Test]
    public async Task Raw_Null_ValueIsEmptyString()
    {
        await Assert.That(H.Raw(null).Value).IsEqualTo("");
        await Assert.That(H.Raw(null).ToString()).IsEqualTo("");
    }

    /// <summary>Inside a normal element, a null Raw is empty content — one tag pair, nothing else.</summary>
    [Test]
    public async Task Raw_Null_InContainer_IsEmptyContent()
    {
        await Assert.That(H.Div(H.Raw(null))).IsEqualTo("<div></div>");
        await Assert.That(H.Span(H.Raw(null))).IsEqualTo("<span></span>");
        await Assert.That(H.P(H.Raw(null))).IsEqualTo("<p></p>");
        await Assert.That(H.Tag("section", H.Raw(null))).IsEqualTo("<section></section>");
        await Assert.That(H.TextArea(H.Raw(null))).IsEqualTo("<textarea></textarea>");
    }

    /// <summary>In a void element, a null Raw self-closes: exactly one tag, never <c>&lt;/br&gt;</c>.</summary>
    [Test]
    public async Task Raw_Null_InVoidElement_SelfCloses()
    {
        var br = H.Tag("br", H.Raw(null));
        await Assert.That(br).IsEqualTo("<br />");
        await Assert.That(br.Contains("</br>", StringComparison.OrdinalIgnoreCase)).IsFalse();

        var img = H.Tag("img", H.Raw(null));
        await Assert.That(img).IsEqualTo("<img />");
        await Assert.That(img.Contains("</img>", StringComparison.OrdinalIgnoreCase)).IsFalse();

        await Assert.That(H.Tag("hr", H.Raw(null))).IsEqualTo("<hr />");
    }

    /// <summary>An explicitly empty Raw behaves exactly like the null one (same "no content" meaning).</summary>
    [Test]
    public async Task Raw_Empty_MatchesNull()
    {
        await Assert.That(H.Div(H.Raw(""))).IsEqualTo("<div></div>");
        await Assert.That(H.Tag("br", H.Raw(""))).IsEqualTo("<br />");
        await Assert.That(H.Tag("img", H.Raw(""))).IsEqualTo("<img />");
    }

    /// <summary>Escaped text content is empty in the same way, so it must self-close too.</summary>
    [Test]
    public async Task EmptyTextContent_OnVoidElement_SelfCloses()
    {
        await Assert.That(H.Tag("br", "")).IsEqualTo("<br />");
        await Assert.That(H.Tag("br", (string?)null)).IsEqualTo("<br />");
    }

    /// <summary>Non-empty markup is still emitted verbatim (no regression on the Raw contract).</summary>
    [Test]
    public async Task Raw_NonEmpty_StillVerbatim()
    {
        await Assert.That(H.Div(H.Raw("<b>x</b>"))).IsEqualTo("<div><b>x</b></div>");
        await Assert.That(H.Tag("br", H.Raw("<b>x</b>"))).IsEqualTo("<br><b>x</b></br>");
    }
}
