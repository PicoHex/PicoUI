namespace PicoHtmx;

/// <summary>HX-Request-aware response shaping for htmx + hx-boost apps.</summary>
public static class HtmxNavigation
{
    /// <summary>Returns <paramref name="fragment"/> to htmx requests and the full
    /// page to everything else. Both variants are served from the SAME URL, so the
    /// response always carries <c>Vary: HX-Request</c> — without it a shared cache
    /// hands the full document to an htmx request (the whole page is swapped into
    /// <c>#main</c>) or the bare fragment to a browser (blank page).</summary>
    public static HtmxResult PageOrFragment(
        WebContext ctx,
        string fragment,
        Func<string> renderFullPage
    )
    {
        var result = ctx.Request.Headers.TryGetValue("HX-Request", out _)
            ? new HtmxResult(fragment)
            : new HtmxResult(renderFullPage());
        return result.WithVary("HX-Request");
    }

    /// <summary>Real 302 redirect — for plain browser GETs (address bar / refresh).
    /// htmx-only navigation should use <see cref="Htmx.Redirect"/> (HX-Redirect header).</summary>
    public static HttpResponse RealRedirect(string url) => WebResults.Redirect(url);
}
