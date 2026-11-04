// PicoHtmx usage sample: a server-rendered htmx page on PicoWeb.
// GET /                     full page (Layout.Page + Layout.AppShell)
// GET /fragments/hello      htmx fragment (bare HTML for swaps)
//
// Escaping contract (see src/PicoHtmx/HtmlBuilder.cs): every builder above the
// `Raw` channel treats its string content argument as TEXT and escapes it.
// Markup is only emitted verbatim through the explicit H.Raw(...) wrapper, so
// nesting builder output inside another builder is always visible at the call
// site — a misplaced `H.Raw` is a review finding, a missing one is a bug.
var app = new WebApp(new SvcContainer(), new WebAppOptions());

app.MapGet(
    "/",
    static (_, _) =>
    {
        var page = Layout.Page(
            "PicoHtmx Sample",
            // Layout.Page/AppShell interpolate their body verbatim (they compose
            // whole documents), so no H.Raw is needed for their arguments.
            Layout.AppShell(
                H.Div("PicoHtmx", new { @class = "logo" }) + H.P("server-rendered"),
                H.Div(
                    // Concatenated builder output is MARKUP, not text: wrap it in
                    // H.Raw, otherwise the tags render as visible entities.
                    H.Raw(
                        H.Div("Welcome", new { @class = "list-title" })
                            + H.P("This page is built with PicoHtmx on PicoWeb — no JS framework.")
                            + H.Button(
                                "Say hi",
                                new
                                {
                                    hx_get = "/fragments/hello",
                                    hx_target = "#hello",
                                    hx_swap = "innerHTML",
                                }
                            )
                            + H.Div("", new { id = "hello" })
                    )
                )
            )
        );
        return new ValueTask<HttpResponse>(Htmx.Html(page));
    }
);

// A fragment assembled from text: H.P escapes its argument, which is what an
// echo handler wants — no H.Raw anywhere near request data.
app.MapGet(
    "/fragments/hello",
    static (_, _) => new ValueTask<HttpResponse>(Htmx.Html(H.P("Hi from the server! 👋")))
);

var server = new WebServer(
    app,
    new WebServerOptions { Endpoint = new IPEndPoint(IPAddress.Loopback, 0) }
);
await server.StartAsync(CancellationToken.None);
var port = ((IPEndPoint)server.LocalEndPoint!).Port;
Console.WriteLine($"PicoHtmx.Sample listening on http://127.0.0.1:{port} (Ctrl+C to stop).");

// Keep serving until Ctrl+C (works with redirected stdin too — no ReadLine).
var stop = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.TrySetResult();
};
await stop.Task;
await server.StopAsync(CancellationToken.None);
