namespace PicoTui.Tests.Screen;

/// <summary>
/// Terminal output must neutralize control characters coming from rendered
/// content (LLM/tool output is untrusted: ESC can retitle the window, clear the
/// screen, or write the clipboard) and must not emit lone surrogates.
/// </summary>
public sealed class ScreenRendererSafetyTests
{
    private sealed class FixedLines(string[] lines) : IComponent
    {
        public string[] Render(int width) => lines;

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task ControlCharacters_AreNeutralized()
    {
        var vt = new VirtualTerminal(20, 2);

        new ScreenRenderer(vt).Render(new FixedLines(["a\x1b]0;PWNED\ab", "c\rd"]));

        await Assert.That(vt.Output).DoesNotContain("\x1b]");
        await Assert.That(vt.Output).DoesNotContain("\x07");
        await Assert.That(vt.Output).DoesNotContain("c\r");
        await Assert.That(vt.Output).Contains("b");
        await Assert.That(vt.Output).Contains("d");
    }

    [Test]
    public async Task LongLine_TruncatesAtSurrogateBoundary()
    {
        var vt = new VirtualTerminal(3, 1);

        new ScreenRenderer(vt).Render(new FixedLines(["ab😀cd"]));

        var output = vt.Output;
        for (var i = 0; i < output.Length; i++)
            if (char.IsHighSurrogate(output[i]))
                await Assert
                    .That(i + 1 < output.Length && char.IsLowSurrogate(output[i + 1]))
                    .IsTrue();
    }
}
