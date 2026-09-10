namespace PicoHtmx;

/// <summary>
/// htmx form/URL helpers: urlencoded body parsing with a hard size ceiling
/// (413 via <see cref="FormBodyTooLargeException"/>), query-string and route
/// value lookups. Parsing is pure and AOT-safe (no reflection).
/// </summary>
public static class HtmxForms
{
    /// <summary>Body ceiling (1MB) — big enough for pasted code/messages, small
    /// enough to keep OOM-DoS protection.</summary>
    public const int MaxFormBodyBytes = 1024 * 1024;

    /// <summary>Decodes one urlencoded form value (+ → space, percent-decoded).</summary>
    public static string DecodeFormValue(string encoded) =>
        Uri.UnescapeDataString(encoded.Replace("+", " "));

    /// <summary>Parses an urlencoded body; duplicate keys collapse (last wins).</summary>
    public static Dictionary<string, string> ParseUrlEncodedBody(string raw)
    {
        var dict = new Dictionary<string, string>();
        foreach (var pair in raw.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
                continue;
            var key = DecodeFormValue(pair[..eq]);
            var val = DecodeFormValue(pair[(eq + 1)..]);
            dict[key] = val;
        }
        return dict;
    }

    /// <summary>Reads the request body as urlencoded form data, enforcing the
    /// 1MB ceiling (throws <see cref="FormBodyTooLargeException"/> on over-limit).</summary>
    public static async Task<Dictionary<string, string>> ReadUrlEncodedBodyAsync(
        WebContext ctx,
        CancellationToken ct
    )
    {
        using var reader = new StreamReader(
            ctx.Request.BodyStream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false
        );
        var buffer = new char[MaxFormBodyBytes + 1];
        var totalRead = 0;
        int charsRead;
        while (
            (charsRead = await reader.ReadAsync(buffer, totalRead, buffer.Length - totalRead)) > 0
        )
        {
            totalRead += charsRead;
            if (totalRead > MaxFormBodyBytes)
                throw new FormBodyTooLargeException(MaxFormBodyBytes);
        }
        return ParseUrlEncodedBody(new string(buffer, 0, totalRead));
    }

    /// <summary>First value of a query-string key (UnescapeDataString-decoded), or null.</summary>
    public static string? QueryParam(string qs, string key)
    {
        var prefix = key + "=";
        var idx = qs.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0)
            return null;
        var val = qs[(idx + prefix.Length)..];
        var ampIdx = val.IndexOf('&');
        return ampIdx >= 0 ? Uri.UnescapeDataString(val[..ampIdx]) : Uri.UnescapeDataString(val);
    }

    /// <summary>Route value parsed as Guid; <see cref="Guid.Empty"/> when missing/unparseable.</summary>
    public static Guid ParseGuid(WebContext ctx, string key)
    {
        ctx.RouteValues.TryGetValue(key, out var v);
        return Guid.TryParse(v, out var g) ? g : Guid.Empty;
    }
}

/// <summary>
/// Mapped to HTTP 413 Payload Too Large by the exception handler (never a
/// 500 — the client sent a valid request that was merely too big).
/// </summary>
public sealed class FormBodyTooLargeException(int maxBytes)
    : Exception($"Form body exceeds maximum size of {maxBytes / 1024}KB")
{
    public int MaxBytes { get; } = maxBytes;
}
