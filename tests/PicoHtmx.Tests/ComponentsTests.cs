namespace PicoHtmx.Tests;

public sealed class ComponentsTests
{
    [Test]
    public async Task ListPage_ShowsEmptyMessage()
    {
        var html = Ux.ListPage("Sessions", Array.Empty<string>(), s => H.P(s));
        await Assert.That(html).Contains("No items");
    }

    [Test]
    public async Task ListPage_RendersItems()
    {
        var items = new[] { "a", "b" };
        var html = Ux.ListPage("Test", items, s => H.P(s));
        await Assert.That(html).Contains("<p>a</p>");
        await Assert.That(html).Contains("<p>b</p>");
    }

    [Test]
    public async Task ListPage_IncludesHeader()
    {
        var html = Ux.ListPage("MyList", new[] { "x" }, s => H.P(s));
        await Assert.That(html).Contains("MyList");
    }

    [Test]
    public async Task Form_IncludesAction()
    {
        var html = Ux.Form("/submit", "post");
        await Assert.That(html).Contains("action=\"/submit\"");
    }

    [Test]
    public async Task Form_IncludesMethod()
    {
        var html = Ux.Form("/s", "post");
        await Assert.That(html).Contains("method=\"post\"");
    }

    [Test]
    public async Task Form_ShowsError()
    {
        var html = Ux.Form("/s", "post", error: "Invalid input");
        await Assert.That(html).Contains("Invalid input");
    }

    [Test]
    public async Task FormField_RendersLabelAndInput()
    {
        var html = Ux.FormField("Name", "name");
        await Assert.That(html).Contains("<label");
        await Assert.That(html).Contains("name=\"name\"");
    }

    // Review 01-C1: type:"textarea" used to emit <input type="textarea">. An input
    // in the Text state DELETES newlines from its value, so editing a multi-line
    // role/cron prompt and saving wrote the collapsed single line back to the
    // config — silent user-data corruption (the only data-loss defect in review).
    [Test]
    public async Task FormField_Textarea_RendersRealTextareaWithNewlines()
    {
        var html = Ux.FormField("Prompt", "prompt", value: "a\nb", type: "textarea");

        await Assert.That(html).Contains("<textarea");
        await Assert.That(html).Contains("</textarea>");
        await Assert.That(html).Contains("a\nb");
        await Assert.That(html).DoesNotContain("type=\"textarea\"");
        await Assert.That(html).DoesNotContain("<input");
    }

    [Test]
    public async Task FormField_Textarea_EscapesContent()
    {
        var html = Ux.FormField("Prompt", "prompt", value: "<b> & \"q\"", type: "textarea");

        await Assert.That(html).Contains("&lt;b&gt; &amp; &quot;q&quot;");
        await Assert.That(html).DoesNotContain("<b>");
    }

    [Test]
    public async Task FormField_Textarea_KeepsRequiredAndName()
    {
        var html = Ux.FormField(
            "Prompt",
            "prompt",
            value: "x",
            type: "textarea",
            required: true
        );

        await Assert.That(html).Contains("id=\"prompt\"");
        await Assert.That(html).Contains("name=\"prompt\"");
        await Assert.That(html).Contains("required");
    }

    [Test]
    public async Task FormField_Text_StillRendersInput()
    {
        var html = Ux.FormField("Name", "name", type: "text");

        await Assert.That(html).Contains("<input");
        await Assert.That(html).Contains("type=\"text\"");
        await Assert.That(html).DoesNotContain("<textarea");
    }

    [Test]
    public async Task ConfirmDialog_ContainsConfirmUrl()
    {
        var html = Ux.ConfirmDialog("Delete?", "/delete/1");
        await Assert.That(html).Contains("hx-post=\"/delete/1\"");
    }

    [Test]
    public async Task Toast_HasCorrectType()
    {
        var html = Ux.Toast("Saved", isError: false);
        await Assert.That(html).Contains("class=\"toast success\"");
    }

    [Test]
    public async Task Toast_Error_HasErrorClass()
    {
        var html = Ux.Toast("Failed", isError: true);
        await Assert.That(html).Contains("class=\"toast error\"");
    }
}
