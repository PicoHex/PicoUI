namespace PicoHtmx;

/// <summary>
/// HX-Trigger payload builders. Toast convention:
/// {"toast":{"value":{message,error}}} — the app body listener reads
/// evt.detail.value.message (htmx beta5 detail shape).
/// Serialization goes through PicoJetson.JsonWriter (AOT-safe, single
/// RFC 8259 escaping implementation) — never reflection-based JsonSerializer.
/// </summary>
public static class HtmxEvents
{
    public static string ToastPayload(string message, bool isError = false) =>
        ValueToJson(
            new Dictionary<string, object?>
            {
                ["toast"] = new Dictionary<string, object?>
                {
                    ["value"] = new Dictionary<string, object?>
                    {
                        ["message"] = message,
                        ["error"] = isError,
                    },
                },
            }
        );

    /// <summary>Event-only payload; empty payload renders {} (never null — htmx
    /// beta5 treats typeof null === "object" as a payload and breaks).</summary>
    public static string EventPayload(string eventName, object? payload = null) =>
        ValueToJson(
            new Dictionary<string, object?>
            {
                [eventName] = payload ?? new Dictionary<string, object?>(),
            }
        );

    /// <summary>Toast + one event in a single HX-Trigger payload.</summary>
    public static string ToastAndEventPayload(string message, bool isError, string eventName) =>
        ValueToJson(
            new Dictionary<string, object?>
            {
                ["toast"] = new Dictionary<string, object?>
                {
                    ["value"] = new Dictionary<string, object?>
                    {
                        ["message"] = message,
                        ["error"] = isError,
                    },
                },
                [eventName] = new Dictionary<string, object?>(),
            }
        );

    private static string ValueToJson(object? value)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        var w = new JsonWriter(buffer);
        WriteValue(ref w, value);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteValue(ref JsonWriter w, object? v)
    {
        switch (v)
        {
            case null:
                w.WriteNull();
                break;
            case string s:
                w.WriteString(s);
                break;
            case bool b:
                w.WriteBoolean(b);
                break;
            case int i:
                w.WriteNumber(i);
                break;
            case long l:
                w.WriteNumber(l);
                break;
            case double d:
                w.WriteNumber(d);
                break;
            case decimal m:
                w.WriteNumber(m);
                break;
            case Dictionary<string, object?> dict:
                w.WriteStartObject();
                foreach (var (k, val) in dict)
                {
                    w.WritePropertyName(k);
                    WriteValue(ref w, val);
                }
                w.WriteEndObject();
                break;
            case List<object?> list:
                w.WriteStartArray();
                foreach (var item in list)
                    WriteValue(ref w, item);
                w.WriteEndArray();
                break;
            case object[] arr:
                w.WriteStartArray();
                foreach (var item in arr)
                    WriteValue(ref w, item);
                w.WriteEndArray();
                break;
            default:
                w.WriteString(v.ToString() ?? "");
                break;
        }
    }
}
