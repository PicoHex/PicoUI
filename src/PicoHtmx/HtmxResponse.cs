namespace PicoHtmx;

public static class Htmx
{
    /// <summary>HTML response whose body depends on the <c>HX-Request</c> header
    /// (htmx swaps the fragment; a browser GET gets the full page). The
    /// <c>Vary</c> header is mandatory here: without it a shared cache serves the
    /// full page to an htmx request (the whole document lands inside the swap
    /// target) or the fragment to a browser (blank page).</summary>
    public static HttpResponse Html(string body, int code = 200)
    {
        var resp = new HttpResponse { StatusCode = code, Body = Encoding.UTF8.GetBytes(body) };
        resp.Headers.Add("Content-Type", "text/html; charset=utf-8");
        resp.Headers.Add("Vary", "HX-Request");
        return resp;
    }

    /// <summary>Frames <paramref name="html"/> as one SSE event.</summary>
    /// <remarks>
    /// <para>An SSE consumer treats <c>\r\n</c>, a bare <c>\r</c> AND <c>\n</c> as line
    /// terminators, so all of them are normalized to <c>\n</c> BEFORE the <c>data:</c>
    /// prefix is applied. Prefixing only <c>\n</c> (the old behaviour) left the tail of a
    /// CR-terminated line unprefixed — and therefore dropped by the client — so
    /// normalization is mandatory.</para>
    /// <para>Line endings are CRLF NOISE, not content: a run of CRs (and an optional
    /// trailing LF) is ONE terminator, so <c>"a\r\rb"</c> is two payload lines. Blank
    /// lines that come from real <c>\n\n</c> ARE content: SSE defines <c>data:</c> with an
    /// empty value as an empty field, so they are framed as their own <c>data:</c> line and
    /// survive to the client. Dropping them (the old behaviour) glued markdown paragraph
    /// breaks back together. A TRAILING terminator only ends the payload — it does not add
    /// a blank line — and the frame's sole blank line is the terminating one, so a payload
    /// line can never end the event early.</para>
    /// </remarks>
    public static HttpResponse SseHtml(string html)
    {
        var framed = new StringBuilder();
        foreach (var line in SsePayloadLines(html))
            framed.Append("data: ").Append(line).Append('\n');
        if (framed.Length == 0)
            framed.Append("data: \n");
        framed.Append('\n');

        var resp = new HttpResponse
        {
            StatusCode = 200,
            Body = Encoding.UTF8.GetBytes(framed.ToString()),
        };
        resp.Headers.Add("Content-Type", "text/event-stream");
        resp.Headers.Add("Cache-Control", "no-cache");
        return resp;
    }

    /// <summary>
    /// Splits an SSE payload into its lines: CRLF and lone CR become <c>\n</c>, a run of
    /// CRs collapses to a single terminator, interior blank lines are kept (as empty
    /// strings) because they are real empty data fields, and one trailing terminator is
    /// consumed instead of producing a phantom blank line.
    /// </summary>
    private static List<string> SsePayloadLines(string html)
    {
        var normalized = new StringBuilder(html.Length);
        for (var i = 0; i < html.Length; i++)
        {
            if (html[i] != '\r')
            {
                normalized.Append(html[i]);
                continue;
            }
            // A CR run (with at most one LF behind it) is ONE terminator.
            while (i + 1 < html.Length && html[i + 1] == '\r')
                i++;
            if (i + 1 < html.Length && html[i + 1] == '\n')
                i++;
            normalized.Append('\n');
        }

        var lines = new List<string>(normalized.ToString().Split('\n'));
        if (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
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

    /// <summary>Trigger a client-side event. <paramref name="eventName"/> builds
    /// the htmx event payload (<c>{"name":{}}</c>); pass <paramref name="data"/> to
    /// send a pre-serialized payload instead.</summary>
    public static HttpResponse Trigger(string eventName, string data = "")
    {
        var resp = Html("", 200);
        resp.Headers.Add("HX-Trigger", data.Length > 0 ? data : HtmxEvents.EventPayload(eventName));
        return resp;
    }

    public static HttpResponse Ok()
    {
        return Html("ok", 200);
    }

    /// <summary>Build an out-of-band swap div targeting <paramref name="targetId"/>.
    /// The id is escaped via <see cref="H.E"/>; the html payload is passed through
    /// verbatim (callers escape their own content, or pass <see cref="H.Raw"/>).</summary>
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
