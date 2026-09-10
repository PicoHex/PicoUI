namespace PicoHtmx;

public static class Htmx
{
    public static HttpResponse Html(string body, int code = 200)
    {
        var resp = new HttpResponse { StatusCode = code, Body = Encoding.UTF8.GetBytes(body) };
        resp.Headers.Add("Content-Type", "text/html; charset=utf-8");
        return resp;
    }

    public static HttpResponse SseHtml(string html)
    {
        var resp = new HttpResponse
        {
            StatusCode = 200,
            Body = Encoding.UTF8.GetBytes($"data: {html}\n\n"),
        };
        resp.Headers.Add("Content-Type", "text/event-stream");
        resp.Headers.Add("Cache-Control", "no-cache");
        return resp;
    }

    public static HttpResponse Redirect(string url)
    {
        var resp = Html("", 200);
        resp.Headers.Add("HX-Redirect", url);
        return resp;
    }

    public static HttpResponse Refresh()
    {
        var resp = Html("", 200);
        resp.Headers.Add("HX-Refresh", "true");
        return resp;
    }

    public static HttpResponse Trigger(string eventName, string data = "")
    {
        var resp = Html("", 200);
        resp.Headers.Add("HX-Trigger", data);
        return resp;
    }

    public static HttpResponse Ok()
    {
        return Html("ok", 200);
    }

    /// <summary>Build an out-of-band swap div targeting <paramref name="targetId"/>.
    /// The id is escaped via <see cref="H.E"/>; the html payload is passed through
    /// verbatim (callers escape their own content).</summary>
    public static string Oob(string targetId, string html) =>
        $"<div id=\"{H.E(targetId)}\" hx-swap-oob=\"true\">{html}</div>";

    /// <summary>OOB chat textarea — carries the id, placeholder, name and row
    /// count directly on the textarea. The id and hx-swap-oob MUST live on the
    /// textarea itself: wrapping it in a div with the target id makes htmx
    /// replace the original textarea with the div, so the replacement textarea
    /// loses the id — Enter-to-send / autogrow handlers (which match
    /// ta.id === "chat-input") silently stop working. Id/name/placeholder are
    /// escaped via <see cref="H.E"/>.</summary>
    public static string OobTextArea(
        string targetId,
        string placeholder,
        string name = "message",
        int rows = 1
    ) =>
        $"<textarea id=\"{H.E(targetId)}\" name=\"{H.E(name)}\" rows=\"{rows}\" placeholder=\"{H.E(placeholder)}\" hx-swap-oob=\"true\"></textarea>";

    /// <summary>Bootstrap script constants for the htmx app shell.</summary>
    public static class Scripts
    {
        /// <summary>htmx v4 uses a configurable meta-character for attribute
        /// names; the Htmx sample app uses '-' (e.g. hx-sse-connect). This script
        /// must run before the htmx bundle initializes.</summary>
        public static string Htmx4Bootstrap() =>
            "<script>htmx.config.metaCharacter=\"-\";</script>";
    }
}
