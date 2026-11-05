namespace PicoHtmx;

/// <summary>
/// A URL the caller has already vetted, produced by <see cref="H.RawUrl"/>. The attribute
/// writer emits it <em>without</em> consulting <see cref="H.Url"/>'s scheme whitelist — this
/// is the one explicit way to say "I know what this is".
/// <para>
/// It is a distinct type, not a <see cref="string"/>, for the same reason
/// <see cref="RawHtml"/> is: an ordinary value can never be mistaken for a vetted one, so
/// the escape hatch cannot be reached by accident (or by a model echoing user data into an
/// attribute bag).
/// </para>
/// <para>
/// Use it ONLY for constants and for values you have sanitized yourself. Everything a user,
/// a request, a database row or a model produced belongs in a plain string, where
/// <see cref="H.Url"/> decides. A <see cref="RawUrl"/> value is still HTML-escaped: this type
/// waives the URL policy, never the attribute escaping.
/// </para>
/// </summary>
public readonly struct RawUrl
{
    internal RawUrl(string? url) => Value = url ?? "";

    /// <summary>The URL exactly as supplied (never scheme-checked by this type).</summary>
    public string Value { get; }

    public override string ToString() => Value;
}
