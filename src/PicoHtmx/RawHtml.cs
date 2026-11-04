namespace PicoHtmx;

/// <summary>
/// Ready-made markup, produced by <see cref="H.Raw"/>. Passing this to a builder
/// is the ONE explicit way to get content emitted verbatim; a plain
/// <see cref="string"/> is always treated as text and escaped
/// (<see cref="H.E"/>). Keeping markup opt-in is what makes
/// <c>H.A(x)</c> and <c>H.Span(x)</c> byte-identical for the same argument.
/// </summary>
public readonly struct RawHtml
{
    internal RawHtml(string? html) => Value = html ?? "";

    /// <summary>The markup exactly as supplied (never escaped by this type).</summary>
    public string Value { get; }

    public override string ToString() => Value;
}
