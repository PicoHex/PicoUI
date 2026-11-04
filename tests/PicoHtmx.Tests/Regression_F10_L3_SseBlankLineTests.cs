namespace PicoHtmx.Tests;

/// <summary>
/// F10a-3 — <c>Htmx.SseHtml</c> dropped EVERY blank payload line, so a markdown
/// paragraph break inside streamed HTML arrived as two adjacent lines (the client
/// joined them with a space and the paragraph structure vanished). SSE gives
/// <c>data:</c> with an empty value a defined meaning — an empty field — so a real
/// blank line in the payload is legitimate content and must survive as its own
/// <c>data:</c> line. A <c>\r\r</c> run is CRLF noise (one line break), so it must
/// NOT invent an extra line.
/// </summary>
public sealed class Regression_F10_L3_SseBlankLineTests
{
    private static string Frame(string html) =>
        Encoding.UTF8.GetString(Htmx.SseHtml(html).Body.ToArray());

    /// <summary>A real blank line is content: three payload lines, three <c>data:</c> lines.</summary>
    [Test]
    public async Task RealBlankLine_IsPreservedAsEmptyDataField()
    {
        await Assert.That(Frame("a\n\nb")).IsEqualTo("data: a\ndata: \ndata: b\n\n");
    }

    /// <summary>Windows line endings: CRLF CRLF is still one real blank line.</summary>
    [Test]
    public async Task CrlfBlankLine_IsPreservedAsEmptyDataField()
    {
        await Assert.That(Frame("a\r\n\r\nb")).IsEqualTo("data: a\ndata: \ndata: b\n\n");
    }

    /// <summary><c>\r\r</c> is a single line break after normalization — no phantom blank line.</summary>
    [Test]
    public async Task CrCrRun_IsOneLineBreak_NotABlankLine()
    {
        var body = Frame("a\r\rb");

        await Assert.That(body).IsEqualTo("data: a\ndata: b\n\n");
        await Assert.That(body.Contains("data: \n", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>A trailing terminator ends the payload; it is not an extra blank line.</summary>
    [Test]
    public async Task TrailingNewline_DoesNotAddBlankLine()
    {
        await Assert.That(Frame("a\n")).IsEqualTo("data: a\n\n");
        await Assert.That(Frame("a\r\n")).IsEqualTo("data: a\n\n");
        await Assert.That(Frame("a\r\r")).IsEqualTo("data: a\n\n");
    }

    /// <summary>Leading blank lines are preserved too (they are real empty fields).</summary>
    [Test]
    public async Task LeadingBlankLine_IsPreserved()
    {
        await Assert.That(Frame("\na")).IsEqualTo("data: \ndata: a\n\n");
    }

    /// <summary>An empty payload still produces one (empty) frame — never a bare terminator.</summary>
    [Test]
    public async Task EmptyPayload_ProducesOneEmptyDataLine()
    {
        await Assert.That(Frame("")).IsEqualTo("data: \n\n");
    }

    /// <summary>The only blank line in the body is the frame terminator: no early dispatch.</summary>
    [Test]
    public async Task OnlyTerminatorIsBlank_NoEventInjection()
    {
        // "y\n\r\r" is a real LF (a blank line) followed by a CR run (one break), so the
        // payload has an empty field in it — framed as "data: ", which is content, NOT a
        // blank line. The body's only "\n\n" is still the frame terminator.
        var body = Frame("x\r\rdata: evil\ny\n\r\rdone");

        await Assert.That(body.IndexOf("\n\n", StringComparison.Ordinal))
            .IsEqualTo(body.Length - 2);
        await Assert.That(body).IsEqualTo(
            "data: x\ndata: data: evil\ndata: y\ndata: \ndata: done\n\n");
    }
}
