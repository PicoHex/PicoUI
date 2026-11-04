namespace PicoHtmx.Tests;

public sealed class HtmxResultTests
{
    private static HttpRequest CreateRequest() =>
        new()
        {
            Method = "GET",
            Target = "/",
            Path = "/",
        };

    [Test]
    public async Task HtmxResult_ReturnsHtmlWithHtmxContentType()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("<div>content</div>");

        var response = result.Execute(ctx);

        await Assert.That(response.StatusCode).IsEqualTo(200);
        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("Content-Type", "text/html; charset=utf-8"));
        var body = Encoding.UTF8.GetString(response.Body.Span);
        await Assert.That(body).IsEqualTo("<div>content</div>");
    }

    [Test]
    public async Task HtmxResult_WithRedirect_AddsHxRedirectHeader()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("").WithRedirect("/dashboard");

        var response = result.Execute(ctx);

        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("HX-Redirect", "/dashboard"));
    }

    [Test]
    public async Task HtmxResult_WithTrigger_AddsHxTriggerHeader()
    {
        var ctx = WebContext.Create(CreateRequest());
        var triggerJson = """{"toast":{"message":"saved"}}""";
        var result = new HtmxResult("").WithTrigger(triggerJson);

        var response = result.Execute(ctx);

        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("HX-Trigger", triggerJson));
    }

    [Test]
    public async Task HtmxResult_WithPushUrl_AddsHxPushUrlHeader()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("").WithPushUrl("/new-url");

        var response = result.Execute(ctx);

        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("HX-Push-Url", "/new-url"));
    }

    [Test]
    public async Task HtmxResult_WithRefresh_AddsHxRefreshHeader()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("").WithRefresh();

        var response = result.Execute(ctx);

        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("HX-Refresh", "true"));
    }

    // Review 01-H4 / §5-#9: this test used to assert that WithRetry added an
    // "HX-Retry" header — i.e. it froze a header htmx does not know as a
    // contract, so the no-op API looked verified. WithRetry is gone; the real
    // swap-control headers replace it.
    [Test]
    public async Task HtmxResult_WithReswap_AddsRealHeader()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("").WithReswap("none").WithRetarget("#main");

        var response = result.Execute(ctx);

        await Assert.That(response.Headers["HX-Reswap"]).IsEqualTo("none");
        await Assert.That(response.Headers["HX-Retarget"]).IsEqualTo("#main");
        await Assert.That(response.Headers.Contains(new KeyValuePair<string, string>("HX-Retry", "false"))).IsFalse();
    }

    [Test]
    public async Task HtmxResult_WithHeader_AddsCustomHeader()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("").WithHeader("X-Custom", "value");

        var response = result.Execute(ctx);

        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("X-Custom", "value"));
    }

    [Test]
    public async Task HtmxResult_ChainedHeaders_AllApplied()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("<p>ok</p>").WithRedirect("/next").WithTrigger("");

        var response = result.Execute(ctx);

        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("HX-Redirect", "/next"));
        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("HX-Trigger", ""));
        await Assert
            .That(response.Headers)
            .Contains(new KeyValuePair<string, string>("Content-Type", "text/html; charset=utf-8"));
    }
}
