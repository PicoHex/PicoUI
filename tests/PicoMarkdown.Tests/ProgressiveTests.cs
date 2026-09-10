namespace PicoMarkdown.Tests;

public sealed class ProgressiveTests
{
    [Test]
    public async Task OpenParagraph_HasLastStableLineBeforeIt()
    {
        var doc = Markdown.Parse("stable\n\nstreaming"); // lines 0-2

        await Assert.That(doc.LastStableLine).IsEqualTo(1); // blank line at 1
    }

    [Test]
    public async Task OpenCodeFence_IsNotStable()
    {
        var doc = Markdown.Parse("before\n\n```\ncode"); // fence at 2, unclosed

        await Assert.That(doc.LastStableLine).IsEqualTo(1);
    }

    [Test]
    public async Task ClosedCodeFence_IsStable()
    {
        var doc = Markdown.Parse("```\ncode\n```");

        await Assert.That(doc.LastStableLine).IsEqualTo(2);
    }

    [Test]
    public async Task FullyClosedDocument_StableToLastContentLine()
    {
        var doc = Markdown.Parse("# hi\n\ntext\n");

        await Assert.That(doc.LastStableLine).IsEqualTo(3);
    }
}
