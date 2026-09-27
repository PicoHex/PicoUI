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
}
