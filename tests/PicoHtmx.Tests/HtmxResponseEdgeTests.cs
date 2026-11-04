namespace PicoHtmx.Tests;

/// <summary>Edge cases found in review: ignored trigger name, multi-line SSE framing.</summary>
public sealed class HtmxResponseEdgeTests
{
    [Test]
    public async Task Trigger_NameOnly_EmitsNamedEventPayload()
    {
        var resp = Htmx.Trigger("sessionCreated");

        await Assert.That(resp.Headers["HX-Trigger"]).IsEqualTo("{\"sessionCreated\":{}}");
    }

    [Test]
    public async Task Trigger_WithData_KeepsRawDataSemantics()
    {
        var resp = Htmx.Trigger("chatCreated", "{id:1}");

        await Assert.That(resp.Headers["HX-Trigger"]).IsEqualTo("{id:1}");
    }

    [Test]
    public async Task SseHtml_SingleLine_Frame()
    {
        var body = Encoding.UTF8.GetString(Htmx.SseHtml("<div>x</div>").Body.ToArray());

        await Assert.That(body).IsEqualTo("data: <div>x</div>\n\n");
    }

    [Test]
    public async Task SseHtml_MultiLine_PrefixesEveryLine()
    {
        var body = Encoding.UTF8.GetString(Htmx.SseHtml("<div>\nline2\n</div>").Body.ToArray());

        await Assert.That(body).IsEqualTo("data: <div>\ndata: line2\ndata: </div>\n\n");
    }

    // Review 01-H3: the SSE parser treats a bare CR (and CRLF) as a line terminator
    // too. Prefixing only "\n" left the payload after a bare CR unprefixed (the
    // client drops it) and a bare "\r\r" produced a BLANK line — i.e. an early
    // dispatch that turns the rest of the payload into a new, injected event.
    // Every line ending must therefore be normalized to "\n" BEFORE framing.
    [Test]
    [Arguments("a\nb")]
    [Arguments("a\r\nb")]
    [Arguments("a\rb")]
    [Arguments("a\r\rb")]
    public async Task SseHtml_NormalizesEveryLineEnding(string html)
    {
        var body = Encoding.UTF8.GetString(Htmx.SseHtml(html).Body.ToArray());

        await Assert.That(body).IsEqualTo("data: a\ndata: b\n\n");
    }

    [Test]
    public async Task SseHtml_CrCr_DoesNotEmitBlankLineBeforeEnd()
    {
        var body = Encoding.UTF8.GetString(Htmx.SseHtml("x\r\rdata: evil").Body.ToArray());

        // Exactly one blank line — the frame terminator at the very end. Any
        // earlier "\n\n" would dispatch the event early (frame injection).
        await Assert.That(body.EndsWith("\n\n", StringComparison.Ordinal)).IsTrue();
        await Assert.That(body.IndexOf("\n\n", StringComparison.Ordinal)).IsEqualTo(body.Length - 2);
        await Assert.That(body).IsEqualTo("data: x\ndata: data: evil\n\n");
    }
}
