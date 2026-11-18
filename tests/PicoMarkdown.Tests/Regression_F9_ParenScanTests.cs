namespace PicoMarkdown.Tests;

/// <summary>
/// F9-3 guard (02-H2 residual): the fix linearised the <c>]</c> scan with an
/// incremental cache but left <c>TryParseLinkish</c>'s
/// <c>text.IndexOf(')', close + 2)</c> unbounded. <c>"[a]("</c> is the cheapest way to
/// make the parser re-scan the whole tail for a <c>)</c> that is never there:
/// 205 KB ⇒ 279 ms, 819 KB ⇒ 2.3 s, 1 MB ⇒ 5.8 s (pure quadratic).
/// <para>
/// The guards below cover the same two families the reviewer used, each judged on
/// what it can actually prove. The no-paren family is the complexity guard: it pins
/// the <em>shape</em> of the curve (2N versus N) and the <em>cost</em> of the input
/// (versus an ordinary scan of the same bytes) instead of a wall-clock budget — a
/// budget measures the runner, a comparison measures the algorithm, and only the
/// algorithm is the contract. The one-distant-paren family cannot be quadratic by
/// construction, so there the tree is what gets pinned.
/// See <see cref="ParseTiming"/> — including why the 2x-ratio alone is not enough to
/// catch a re-scan and the ordinary-scan comparison is.
/// </para>
/// </summary>
[NotInParallel] // timing guards: a parallel parse would decide the ratio
public sealed class Regression_F9_ParenScanTests
{
    private static string Repeat(int size) => string.Concat(Enumerable.Repeat("[a](", size / 4));

    [Test]
    public async Task LinkTextFollowedByOpenParen_GrowLinearly_NotQuadratically()
    {
        await Assert.That(Markdown.Parse(Repeat(1024)).Blocks).Count().IsEqualTo(1);

        var scaling = ParseTiming.Measure(Repeat(ParseTiming.Small), Repeat(ParseTiming.Large));

        await Assert
            .That(scaling.Ratio)
            .IsLessThan(ParseTiming.MaxGrowthOnDoubling)
            .Because(scaling.Explain("'[a](' with no ')'"));

        await Assert
            .That(scaling.WorkOfOrdinaryScans)
            .IsLessThan(ParseTiming.MaxWorkOfAnOrdinaryScan)
            .Because(scaling.Explain("'[a](' with no ')'"));

        await Assert
            .That(scaling.DoubledMs)
            .IsLessThan(ParseTiming.CatastropheCeilingMs)
            .Because("the ')' scan must return, not scan forever");
    }

    [Test]
    public async Task OneDistantClosingParen_DestinationIsTheWholeTail_AndReturns()
    {
        // The reviewer's second family, measured honestly: this shape CANNOT be
        // quadratic, with or without the memoisation. The first ')' found is the
        // distant one, so the first link's destination is the entire tail and the
        // same link consumes the rest of the input — one scan, not N. (Verified:
        // with the memoisation removed this input still parses in ~0.2 ms.)
        // What it pins instead is the other half of the fix: the memoisation must be
        // a cache, not a cap. A capped destination length would silently truncate
        // this URL, and a ratio guard would never notice. So does a hang, hence the
        // ceiling.
        var text = Repeat(ParseTiming.Large) + ")";

        var doc = Markdown.Parse(text);
        await Assert.That(doc.Blocks).Count().IsEqualTo(1);

        var link = (Link)((Paragraph)doc.Blocks[0]).Inlines[0];
        await Assert.That(link.Url).IsEqualTo(text[4..^1]); // "[a](" is 4 bytes, ")" one

        var ms = ParseTiming.MedianMs(text);
        await Assert
            .That(ms)
            .IsLessThan(ParseTiming.CatastropheCeilingMs)
            .Because(
                $"{ParseTiming.Large / 1024} KB of \"[a](\" + \")\" took {ms:F1} ms; "
                    + "a number near this ceiling is a hang, not a slow machine"
            );
    }

    [Test]
    public async Task ThePreFixInput_ReturnsAtAll()
    {
        // The size the reviewer measured at 2.3 s under the unbounded scan. This is a
        // HANG detector, not a budget: linear, this costs tens of milliseconds
        // everywhere, and CatastropheCeilingMs is far above any plausible runner. The
        // complexity verdict belongs to the two guards above.
        var text = Repeat(800 * 1024);

        var ms = ParseTiming.MedianMs(text);

        await Assert
            .That(ms)
            .IsLessThan(ParseTiming.CatastropheCeilingMs)
            .Because(
                $"800 KB of \"[a](\" took {ms:F0} ms; "
                    + "a number near this ceiling is a hang, not a slow machine"
            );
    }

    [Test]
    public async Task LinearisationDoesNotChangeTheParsedTree()
    {
        // the cache must be a cache, not a cap: a legitimate long-destination link
        // still parses, and a huge tail is still one text paragraph
        var longUrl = new string('u', 4096);
        var doc = Markdown.Parse($"[label]({longUrl})");

        var paragraph = (Paragraph)doc.Blocks[0];
        var link = (Link)paragraph.Inlines[0];
        await Assert.That(link.Url).IsEqualTo(longUrl);
    }
}
