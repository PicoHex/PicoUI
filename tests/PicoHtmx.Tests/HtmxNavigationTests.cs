namespace PicoHtmx.Tests;

public sealed class HtmxNavigationTests
{
    private static WebContext CtxWith(bool hxRequest)
    {
        var req = new HttpRequest
        {
            Method = "GET",
            Target = "/fragments/chats/1",
            Path = "/fragments/chats/1",
            Headers = hxRequest
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["HX-Request"] = "true",
                }
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        return WebContext.Create(req);
    }

    [Test]
    public async Task PageOrFragment_HxRequest_ReturnsFragment()
    {
        var ctx = CtxWith(hxRequest: true);
        var result = HtmxNavigation.PageOrFragment(ctx, "<p>frag</p>", () => "<html>full</html>");
        await Assert
            .That(result.Execute(ctx).Body)
            .IsEquivalentTo(System.Text.Encoding.UTF8.GetBytes("<p>frag</p>"));
    }

    [Test]
    public async Task PageOrFragment_NoHxRequest_RendersFullPage()
    {
        var ctx = CtxWith(hxRequest: false);
        var result = HtmxNavigation.PageOrFragment(ctx, "<p>frag</p>", () => "<html>full</html>");
        await Assert
            .That(result.Execute(ctx).Body)
            .IsEquivalentTo(System.Text.Encoding.UTF8.GetBytes("<html>full</html>"));
    }

    [Test]
    public async Task RealRedirect_Returns302_WithLocation()
    {
        var resp = HtmxNavigation.RealRedirect("/fragments/chats/1");
        await Assert.That(resp.StatusCode).IsEqualTo(302);
        await Assert.That(resp.Headers["Location"]).IsEqualTo("/fragments/chats/1");
    }
}
