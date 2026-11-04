namespace PicoHtmx;

/// <summary>
/// htmx response: HTML body plus the response headers htmx actually reads.
/// <para><b>Retry/error handling.</b> htmx has NO retry response header — the old
/// <c>WithRetry</c> wrote <c>HX-Retry</c>, which the client silently ignores, so
/// the API looked useful and did nothing (it was removed, and
/// <see cref="WithHeader"/> now rejects unknown <c>HX-*</c> names). The supported
/// ways to steer retries are all client-side: configure
/// <c>htmx.config.responseHandling</c>, listen to
/// <c>htmx:responseError</c>/<c>htmx:sendError</c>, or answer with a custom
/// event through <see cref="WithTrigger"/> (an event handler in the page can then
/// re-issue the request) — e.g. <c>new HtmxResult("").WithTrigger("{\"retry\":{}}")</c>.</para>
/// </summary>
public sealed class HtmxResult : IWebResult
{
    /// <summary>Response headers htmx documents (htmx.org/reference#response_headers).
    /// Anything else spelled <c>HX-…</c> is a no-op on the client, so it is a
    /// programming error rather than a header.</summary>
    private static readonly HashSet<string> KnownHtmxHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "HX-Redirect",
        "HX-Refresh",
        "HX-Location",
        "HX-Push-Url",
        "HX-Replace-Url",
        "HX-Reswap",
        "HX-Retarget",
        "HX-Reselect",
        "HX-Trigger",
        "HX-Trigger-After-Swap",
        "HX-Trigger-After-Settle",
    };

    private readonly string _html;
    private readonly int _statusCode;
    private readonly Dictionary<string, string> _htmxHeaders = new();
    private readonly List<string> _vary = new();

    public HtmxResult(string html, int statusCode = 200)
    {
        _html = html;
        _statusCode = statusCode;
    }

    /// <summary>Client-side redirect (<c>HX-Redirect</c>).</summary>
    public HtmxResult WithRedirect(string url)
    {
        _htmxHeaders["HX-Redirect"] = url;
        return this;
    }

    public HtmxResult WithTrigger(string jsonData)
    {
        _htmxHeaders["HX-Trigger"] = jsonData;
        return this;
    }

    /// <summary>Fires a custom event only after the swap has completed.</summary>
    public HtmxResult WithTriggerAfterSwap(string jsonData)
    {
        _htmxHeaders["HX-Trigger-After-Swap"] = jsonData;
        return this;
    }

    /// <summary>Fires a custom event only after the settle step has completed.</summary>
    public HtmxResult WithTriggerAfterSettle(string jsonData)
    {
        _htmxHeaders["HX-Trigger-After-Settle"] = jsonData;
        return this;
    }

    public HtmxResult WithPushUrl(string url)
    {
        _htmxHeaders["HX-Push-Url"] = url;
        return this;
    }

    public HtmxResult WithReplaceUrl(string url)
    {
        _htmxHeaders["HX-Replace-Url"] = url;
        return this;
    }

    public HtmxResult WithRefresh()
    {
        _htmxHeaders["HX-Refresh"] = "true";
        return this;
    }

    /// <summary>Overrides how the response is swapped (e.g. <c>innerHTML</c>,
    /// <c>outerHTML</c>, <c>none</c>) — the usual way to turn an error response
    /// into "show this, do not swap".</summary>
    public HtmxResult WithReswap(string swapStyle)
    {
        _htmxHeaders["HX-Reswap"] = swapStyle;
        return this;
    }

    /// <summary>Retargets the swap at a different element (CSS selector).</summary>
    public HtmxResult WithRetarget(string selector)
    {
        _htmxHeaders["HX-Retarget"] = selector;
        return this;
    }

    /// <summary>Selects which part of the response is swapped (CSS selector).</summary>
    public HtmxResult WithReselect(string selector)
    {
        _htmxHeaders["HX-Reselect"] = selector;
        return this;
    }

    /// <summary>Client-side "navigate" (htmx's <c>HX-Location</c>): a full
    /// navigation that does not reload the document.</summary>
    public HtmxResult WithLocation(string url)
    {
        _htmxHeaders["HX-Location"] = url;
        return this;
    }

    /// <summary>Adds response header names to <c>Vary</c>. Required for any
    /// response whose body depends on a request header — notably
    /// <c>HX-Request</c>, otherwise a shared cache can serve the full page to an
    /// htmx request (or the fragment to a browser).</summary>
    public HtmxResult WithVary(params string[] headerNames)
    {
        foreach (var name in headerNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _vary.Add(name);
        }
        return this;
    }

    /// <summary>Sets an arbitrary response header. Unknown <c>HX-*</c> names are
    /// rejected: htmx ignores headers it does not know, so a typo would compile,
    /// ship and do nothing.</summary>
    public HtmxResult WithHeader(string name, string value)
    {
        if (
            name.StartsWith("HX-", StringComparison.OrdinalIgnoreCase)
            && !KnownHtmxHeaders.Contains(name)
        )
        {
            throw new ArgumentException(
                $"'{name}' is not an htmx response header; htmx silently ignores unknown HX-* "
                    + "names. Known: "
                    + string.Join(", ", KnownHtmxHeaders),
                nameof(name)
            );
        }
        _htmxHeaders[name] = value;
        return this;
    }

    public HttpResponse Execute(WebContext ctx)
    {
        var resp = new HttpResponse
        {
            StatusCode = _statusCode,
            Body = Encoding.UTF8.GetBytes(_html),
        };
        resp.Headers.Add("Content-Type", "text/html; charset=utf-8");
        if (_vary.Count > 0)
            resp.Headers.Add("Vary", string.Join(", ", _vary));
        foreach (var (key, value) in _htmxHeaders)
            resp.Headers.Add(key, value);
        return resp;
    }
}
