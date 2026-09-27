namespace PicoHtmx.Tests;

/// <summary>
/// HTML only self-closes void elements. A self-closed non-void tag is a parse
/// error: <c>&lt;script src /&gt;</c> swallows the rest of the document,
/// <c>&lt;div /&gt;</c> re-parents the following markup. The builder must emit
/// explicit close tags for non-void elements.
/// </summary>
public sealed class HtmlBuilderVoidElementTests
{
    [Test]
    public async Task NonVoidWithoutContent_RendersExplicitCloseTag()
    {
        await Assert.That(H.Tag("div")).IsEqualTo("<div></div>");
        await Assert.That(H.Tag("textarea")).IsEqualTo("<textarea></textarea>");
        await Assert.That(H.Div()).IsEqualTo("<div></div>");
        await Assert.That(H.Span()).IsEqualTo("<span></span>");
    }

    [Test]
    public async Task VoidWithoutContent_SelfCloses()
    {
        await Assert.That(H.Tag("br")).IsEqualTo("<br />");
        await Assert.That(H.Tag("hr")).IsEqualTo("<hr />");
        await Assert.That(H.Input()).IsEqualTo("<input />");
    }

    [Test]
    public async Task Script_RendersClosedScriptTag()
    {
        await Assert.That(H.Script("/a.js")).IsEqualTo("<script src=\"/a.js\"></script>");
    }

    [Test]
    public async Task LayoutPage_Scripts_RendersClosedScriptTag()
    {
        var html = Layout.Page("T", "B", scripts: "/app.js");
        await Assert.That(html).Contains("<script src=\"/app.js\"></script>");
    }
}

/// <summary>
/// HTML boolean attributes are true when present. Rendering a bool value as
/// "False" inverts the caller's intent (<c>disabled="False"</c> still disables).
/// </summary>
public sealed class HtmlBuilderBooleanAttributeTests
{
    [Test]
    public async Task BoolTrue_RendersValuelessAttribute()
    {
        await Assert.That(H.Input(new { disabled = true })).IsEqualTo("<input disabled=\"\" />");
    }

    [Test]
    public async Task BoolFalse_OmitsAttribute()
    {
        await Assert.That(H.Input(new { disabled = false })).IsEqualTo("<input />");
    }

    [Test]
    public async Task BoolFalse_OmitsAttribute_OnContentElement()
    {
        await Assert.That(H.Div("x", new { hidden = false })).IsEqualTo("<div>x</div>");
    }
}
