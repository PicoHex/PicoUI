namespace PicoHtmx.Tests;

public sealed class HtmxResponseTests
{
    [Test]
    public async Task Html_SetsContentType()
    {
        var resp = Htmx.Html("<div>hello</div>");
        await Assert.That(resp.Headers["Content-Type"]).IsEqualTo("text/html; charset=utf-8");
    }

    [Test]
    public async Task Html_SetsBody()
    {
        var resp = Htmx.Html("<p>test</p>");
        var body = Encoding.UTF8.GetString(resp.Body.ToArray());
        await Assert.That(body).IsEqualTo("<p>test</p>");
    }

    [Test]
    public async Task Html_DefaultStatusCodeIs200()
    {
        var resp = Htmx.Html("ok");
        await Assert.That(resp.StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task Html_CustomStatusCode()
    {
        var resp = Htmx.Html("not found", 404);
        await Assert.That(resp.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task SseHtml_SetsSseContentType()
    {
        var resp = Htmx.SseHtml("<div>stream</div>");
        await Assert.That(resp.Headers["Content-Type"]).IsEqualTo("text/event-stream");
    }

    [Test]
    public async Task Redirect_SetsHxRedirectHeader()
    {
        var resp = Htmx.Redirect("/sessions");
        await Assert.That(resp.Headers["HX-Redirect"]).IsEqualTo("/sessions");
    }

    [Test]
    public async Task Refresh_SetsHxRefreshHeader()
    {
        var resp = Htmx.Refresh();
        await Assert.That(resp.Headers["HX-Refresh"]).IsEqualTo("true");
    }

    [Test]
    public async Task Trigger_SetsHxTriggerHeader()
    {
        var resp = Htmx.Trigger("chatCreated", "{id:1}");
        await Assert.That(resp.Headers["HX-Trigger"]).IsEqualTo("{id:1}");
    }

    [Test]
    public async Task Ok_Returns200WithOkBody()
    {
        var resp = Htmx.Ok();
        await Assert.That(resp.StatusCode).IsEqualTo(200);
        var body = Encoding.UTF8.GetString(resp.Body.ToArray());
        await Assert.That(body).IsEqualTo("ok");
    }

    [Test]
    public async Task Oob_ProducesOutOfBandDiv()
    {
        var html = Htmx.Oob("session-list", "<li>x</li>");
        await Assert
            .That(html)
            .IsEqualTo("<div id=\"session-list\" hx-swap-oob=\"true\"><li>x</li></div>");
    }

    [Test]
    public async Task Oob_EscapesTargetId()
    {
        var html = Htmx.Oob("a\"b", "x");
        await Assert.That(html).DoesNotContain("a\"b");
    }

    [Test]
    public async Task OobTextArea_CarriesPlaceholderAndOob()
    {
        var html = Htmx.OobTextArea("chat-input", "Type a message or drop files — ↑↓ for history");
        await Assert.That(html).Contains("id=\"chat-input\"");
        await Assert.That(html).Contains("hx-swap-oob=\"true\"");
        await Assert.That(html).Contains("Type a message");
    }

    [Test]
    public async Task OobTextArea_PutsIdOnTextarea_NotOnWrapperDiv()
    {
        // Bug: the id sat on the OOB wrapper <div>, so htmx replaced the whole
        // <textarea id="chat-input"> with <div id="chat-input"><textarea>…</textarea></div>.
        // The replacement textarea had NO id → the Enter-to-send / autogrow
        // handlers (which match ta.id === "chat-input") silently stopped working,
        // and the fresh textarea lost the autogrow height (appeared to shrink
        // after send). The id and hx-swap-oob must live ON the textarea.
        var html = Htmx.OobTextArea("chat-input", "Type a message");
        await Assert.That(html).DoesNotContain("<div id=\"chat-input\"");
        await Assert.That(html).Contains("<textarea id=\"chat-input\"");
        await Assert.That(html).Contains("hx-swap-oob=\"true\"");
    }

    [Test]
    public async Task Scripts_Htmx4Bootstrap_SetsMetaCharacter()
    {
        await Assert
            .That(Htmx.Scripts.Htmx4Bootstrap())
            .Contains("htmx.config.metaCharacter=\"-\"");
    }
}
