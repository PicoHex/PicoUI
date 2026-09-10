namespace PicoTui.Tests.Terminal;

/// <summary>
/// audit L9: the OSC title escape (\x1b]0;TITLE\x07) wrote the raw title —
/// session names (user-controlled) could inject terminal control sequences.
/// Titles are sanitized to printable characters before hitting the escape.
/// </summary>
public sealed class TitleSanitizerTests
{
    [Test]
    public async Task Sanitize_StripsEscapeSequences()
    {
        // ESC/BEL are stripped — the payload text between them stays (it is
        // inert once the OSC escape cannot fire), the injection is neutralized.
        await Assert
            .That(TitleSanitizer.Sanitize("hi\u001b]0;pwned\u0007there"))
            .IsEqualTo("hi]0;pwnedthere");
        await Assert
            .That(TitleSanitizer.Sanitize("hi\u001b]0;pwned\u0007there"))
            .DoesNotContain("\u001b");
    }

    [Test]
    public async Task Sanitize_StripsOtherControlChars_KeepsUnicode()
    {
        await Assert.That(TitleSanitizer.Sanitize("chat\u0000\u001f会话")).IsEqualTo("chat会话");
    }

    [Test]
    public async Task Sanitize_KeepsNormalTitles()
    {
        await Assert
            .That(TitleSanitizer.Sanitize("Session 321 — DeepSeek"))
            .IsEqualTo("Session 321 — DeepSeek");
    }
}
