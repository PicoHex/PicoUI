namespace PicoTui.Tests.Layout;

/// <summary>Every component that clips a line to the available width must not
/// split a UTF-16 surrogate pair (invalid output on real terminals).</summary>
public sealed class TruncationSafetyTests
{
    private static async Task AssertNoLoneSurrogates(string[] lines)
    {
        foreach (var line in lines)
        {
            for (var i = 0; i < line.Length; i++)
                if (char.IsHighSurrogate(line[i]))
                    await Assert
                        .That(i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
                        .IsTrue();
            for (var i = 0; i < line.Length; i++)
                if (char.IsLowSurrogate(line[i]))
                    await Assert.That(i > 0 && char.IsHighSurrogate(line[i - 1])).IsTrue();
        }
    }

    [Test]
    public async Task MarkdownCodeBlock_ClipsAtRuneBoundary()
    {
        var m = new MarkdownComponent("```\nab😀cd\n```");

        await AssertNoLoneSurrogates(m.Render(3));
    }

    [Test]
    public async Task ThinkingBlock_ClipsAtRuneBoundary()
    {
        var tb = new ThinkingBlock();
        tb.Append("ab😀cd");
        tb.Toggle();

        await AssertNoLoneSurrogates(tb.Render(3));
    }

    [Test]
    public async Task ToolCallWidget_ClipsAtRuneBoundary()
    {
        var w = new ToolCallWidget();
        w.Start("t");
        w.AppendArgs("ab😀cd");

        await AssertNoLoneSurrogates(w.Render(7));
    }

    [Test]
    public async Task Editor_ClipsAtRuneBoundary()
    {
        var ed = new Editor();
        ed.SetText("ab😀cd");

        var lines = ed.Render(3);

        await Assert.That(lines[0]).IsEqualTo("ab");
        await AssertNoLoneSurrogates(lines);
    }
}
