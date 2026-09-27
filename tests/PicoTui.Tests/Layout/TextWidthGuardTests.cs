namespace PicoTui.Tests.Layout;

/// <summary>
/// Width is caller-supplied. width&lt;=0 must yield no lines (previously
/// <c>Text.Render(0)</c> looped forever because take==0 never advanced).
/// </summary>
public sealed class TextWidthGuardTests
{
    [Test]
    public async Task Render_ZeroWidth_ReturnsNoLines()
    {
        // WaitAsync keeps the RED run finite; after the fix this returns immediately.
        var lines = await Task.Run(() => new Text("abc").Render(0))
            .WaitAsync(TimeSpan.FromSeconds(2));

        await Assert.That(lines.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Render_NegativeWidth_ReturnsNoLines()
    {
        var lines = new Text("abc").Render(-1);

        await Assert.That(lines.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Wrap_BreaksByDisplayWidth_NotByChars()
    {
        // a(1) b(1) 😀(2) c(1) d(1): "ab" | "😀c" | "d" for width 3
        var lines = new Text("ab😀cd").Render(3);

        await Assert.That(lines).IsEquivalentTo(new[] { "ab", "😀c", "d" });
    }

    [Test]
    public async Task Wrap_DoesNotSplitSurrogatePairs()
    {
        // the emoji straddles the width-4 cut (chars 3-4) — must move to the next line whole
        var lines = new Text("xxx😀yyyy").Render(4);

        foreach (var line in lines)
        {
            for (var i = 0; i < line.Length; i++)
                if (char.IsHighSurrogate(line[i]))
                    await Assert
                        .That(i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
                        .IsTrue();
        }
    }

    [Test]
    public async Task Wrap_KeepsExistingHardWrapShape()
    {
        var lines = new Text("hello world").Render(6);

        await Assert.That(lines).IsEquivalentTo(new[] { "hello ", "world" });
    }
}
