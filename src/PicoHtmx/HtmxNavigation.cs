namespace PicoHtmx;

/// <summary>HX-Request-aware response shaping for htmx + hx-boost apps.</summary>
public static class HtmxNavigation
{
    public static HtmxResult PageOrFragment(
        WebContext ctx,
        string fragment,
        Func<string> renderFullPage
    )
    {
        if (ctx.Request.Headers.TryGetValue("HX-Request", out _))
            return new HtmxResult(fragment);
        return new HtmxResult(renderFullPage());
    }

    /// <summary>Real 302 redirect — for plain browser GETs (address bar / refresh).
    /// htmx-only navigation should use <see cref="Htmx.Redirect"/> (HX-Redirect header).</summary>
    public static HttpResponse RealRedirect(string url) => WebResults.Redirect(url);
}
