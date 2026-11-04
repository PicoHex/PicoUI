namespace PicoMarkdown.Tests;

/// <summary>
/// 02-H1 — a backtick-only line shorter than the opening fence must be trimmed
/// <em>only</em> when it is the streaming tail (nothing but blank lines follow).
/// A nested shorter fence in the middle of a block is content (CommonMark).
/// </summary>
public sealed class Regression_02_H1
{
    [Test]
    public async Task NestedShorterFenceInsideFourTickShell_IsPreserved()
    {
        // 4-tick shell wrapping an inner 3-tick fence: the standard way to show a
        // fenced code block inside a fenced code block. Byte-level, all LF.
        var doc = Markdown.Parse("````\n```\ncode\n```\n````\n");

        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Code).IsEqualTo("```\ncode\n```");
        await Assert.That(code.Code.Split('\n')[0]).IsEqualTo("```"); // inner opening fence kept
        await Assert.That(code.Code.Split('\n')[2]).IsEqualTo("```"); // inner closing fence kept
        await Assert.That(code.Code.Split('\n').Length).IsEqualTo(3); // no line lost
        await Assert.That(code.StartLine).IsEqualTo(0);
        await Assert.That(code.EndLine).IsEqualTo(4);
    }

    [Test]
    public async Task ShorterFenceWithCodeAfterIt_IsPreserved()
    {
        var doc = Markdown.Parse("````\n``\nafter\n````");

        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Code).IsEqualTo("``\nafter");
    }

    [Test]
    public async Task ShorterFenceAtStreamingTail_IsStillTrimmed()
    {
        // streaming: "`" then "``" arrive before the closing 4-tick fence.
        var doc = Markdown.Parse("````\ncode\n``");

        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Code).IsEqualTo("code");
    }

    [Test]
    public async Task PartialFenceAtStreamingTailOfFourTickShell_IsStillTrimmed()
    {
        var doc = Markdown.Parse("````\ncode\n```");

        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Code).IsEqualTo("code");
    }

    [Test]
    public async Task ThreeTickShellWithTwoTickLineInTheMiddle_IsPreserved()
    {
        var doc = Markdown.Parse("```\n``\ncode\n```");

        var code = (FencedCode)doc.Blocks.Single();
        await Assert.That(code.Code).IsEqualTo("``\ncode");
    }
}
