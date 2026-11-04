using System.Reflection;

namespace PicoHtmx.Tests;

/// <summary>
/// Review 01-H5 — a response that varies on <c>HX-Request</c> MUST say so.
/// Without <c>Vary: HX-Request</c> a shared cache (PicoNode's CacheMiddleware)
/// hands the full-page variant to an htmx request (the whole
/// <c>&lt;!DOCTYPE html&gt;</c> gets swapped into <c>#main</c>) or the fragment
/// to a browser GET (blank page).
/// </summary>
public sealed class HtmxVaryTests
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

    private static void AssertVariesOnHxRequest(HttpResponse resp)
    {
        var vary = resp.Headers["Vary"];
        if (vary is null)
            throw new InvalidOperationException(
                "response has no Vary header — a body that depends on HX-Request must declare it"
            );
        if (!vary.Contains("HX-Request", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Vary must list HX-Request (case-insensitive), got '{vary}'"
            );
    }

    [Test]
    public async Task PageOrFragment_HxRequest_FragmentVariesOnHxRequest()
    {
        var ctx = CtxWith(hxRequest: true);
        var result = HtmxNavigation.PageOrFragment(ctx, "<p>frag</p>", () => "<html>full</html>");

        AssertVariesOnHxRequest(result.Execute(ctx));
        await Assert.That(result.Execute(ctx).StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task PageOrFragment_NoHxRequest_FullPageVariesOnHxRequest()
    {
        var ctx = CtxWith(hxRequest: false);
        var result = HtmxNavigation.PageOrFragment(ctx, "<p>frag</p>", () => "<html>full</html>");

        AssertVariesOnHxRequest(result.Execute(ctx));
        await Assert.That(result.Execute(ctx).StatusCode).IsEqualTo(200);
    }

    [Test]
    public async Task Html_VariesOnHxRequest()
    {
        AssertVariesOnHxRequest(Htmx.Html("<p>x</p>"));
        await Assert.That(Htmx.Html("<p>x</p>").Headers["Content-Type"]).IsNotNull();
    }
}

/// <summary>
/// Review 01-H4 — <c>HtmxResult</c> used to expose <c>WithRetry</c>, which wrote
/// <c>HX-Retry</c>: a header htmx does not know and silently ignores, so the
/// "public API" did nothing at all. Unknown <c>HX-*</c> headers are rejected now,
/// and the emitted header set is pinned to the real htmx response headers.
/// </summary>
public sealed class HtmxHeaderContractTests
{
    private static HttpRequest CreateRequest() =>
        new()
        {
            Method = "GET",
            Target = "/",
            Path = "/",
        };

    /// <summary>Real htmx response headers (htmx.org/reference#response_headers)
    /// plus the cache headers this library sets itself.</summary>
    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type",
        "Vary",
        "Cache-Control",
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

    [Test]
    public async Task WithHeader_UnknownHxHeader_Throws()
    {
        // "HX-Retry" does not exist in htmx — a typo'd header is silently ignored
        // by the client, so the library must fail loudly instead of shipping a
        // no-op API.
        await Assert
            .That(() => new HtmxResult("").WithHeader("HX-Retry", "false"))
            .Throws<ArgumentException>();
        await Assert
            .That(() => new HtmxResult("").WithHeader("HX-Retryy", "false"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task WithHeader_KnownHxHeaderAndCustomHeader_AreAccepted()
    {
        var ctx = WebContext.Create(CreateRequest());
        var resp = new HtmxResult("")
            .WithHeader("HX-Replace-Url", "/next")
            .WithHeader("X-Custom", "value")
            .Execute(ctx);

        await Assert.That(resp.Headers["HX-Replace-Url"]).IsEqualTo("/next");
        await Assert.That(resp.Headers["X-Custom"]).IsEqualTo("value");
    }

    [Test]
    public async Task HtmxResult_EmittedHeaders_AreAllRealHeaders()
    {
        var ctx = WebContext.Create(CreateRequest());
        var result = new HtmxResult("");
        var invoked = new List<string>();

        // Invoke every public declared "With*" builder with harmless arguments so
        // a newly added (and possibly non-existent) header cannot slip through.
        foreach (
            var method in typeof(HtmxResult).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
            )
        )
        {
            if (!method.Name.StartsWith("With", StringComparison.Ordinal))
                continue;
            // WithHeader takes (name, value) and WithVary takes params string[] —
            // neither can be invoked with the generic "harmless argument" fill-in.
            if (method.Name is "WithHeader" or "WithVary")
                continue;
            var args = method
                .GetParameters()
                .Select(p => p.ParameterType == typeof(bool) ? (object)false : "")
                .ToArray();
            method.Invoke(result, args);
            invoked.Add(method.Name);
        }

        await Assert.That(invoked.Count).IsGreaterThan(0);

        var resp = result.Execute(ctx);
        var offenders = new List<string>();
        foreach (var (name, _) in resp.Headers)
        {
            if (!AllowedHeaders.Contains(name))
                offenders.Add(name);
        }
        // Not a real htmx/cache response header.
        await Assert.That(offenders).IsEmpty();
    }


    [Test]
    public async Task TheRealHtmxHeaders_AreEmitted()
    {
        // Replaces the removed WithRetry (HX-Retry is not an htmx header): the
        // genuinely useful swap-control headers now exist and reach the response.
        var ctx = WebContext.Create(CreateRequest());
        var resp = new HtmxResult("<p>x</p>")
            .WithReswap("none")
            .WithRetarget("#main")
            .WithReselect("#inner")
            .WithLocation("/next")
            .WithReplaceUrl("/next")
            .WithTriggerAfterSwap("{\"saved\":{}}")
            .WithTriggerAfterSettle("{\"settled\":{}}")
            .Execute(ctx);

        await Assert.That(resp.Headers["HX-Reswap"]).IsEqualTo("none");
        await Assert.That(resp.Headers["HX-Retarget"]).IsEqualTo("#main");
        await Assert.That(resp.Headers["HX-Reselect"]).IsEqualTo("#inner");
        await Assert.That(resp.Headers["HX-Location"]).IsEqualTo("/next");
        await Assert.That(resp.Headers["HX-Replace-Url"]).IsEqualTo("/next");
        await Assert.That(resp.Headers["HX-Trigger-After-Swap"]).IsEqualTo("{\"saved\":{}}");
        await Assert.That(resp.Headers["HX-Trigger-After-Settle"]).IsEqualTo("{\"settled\":{}}");
    }

    [Test]
    public async Task WithVary_MergesIntoOneHeader()
    {
        var ctx = WebContext.Create(CreateRequest());
        var resp = new HtmxResult("").WithVary("HX-Request").WithVary("HX-Target").Execute(ctx);

        await Assert.That(resp.Headers["Vary"]).IsEqualTo("HX-Request, HX-Target");
    }

    [Test]
    public async Task PublicApi_HasNoRetrySurface_AndNoHxRetryLiteralInSource()
    {
        var retryMethods = typeof(HtmxResult)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.Contains("Retry", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Name)
            .ToArray();
        // HX-Retry does not exist in htmx; a WithRetry API would be a silent no-op.
        await Assert.That(retryMethods).IsEmpty();

        // Source scan: the literal must not reappear in CODE anywhere in the
        // library (the doc comments of HtmxResult explain why it was removed).
        var srcDir = Path.Combine(RepoRoot(), "src", "PicoHtmx");
        var offenders = Directory
            .EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
                File.ReadLines(f)
                    .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    .Any(line => line.Contains("HX-Retry", StringComparison.Ordinal))
            )
            .ToArray();
        // HX-Retry is not a real header — no file may mention it.
        await Assert.That(offenders).IsEmpty();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "PicoHtmx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("repository root (src/PicoHtmx) not found");
    }
}
