namespace PicoMarkdown.Tests;

/// <summary>
/// 02-H2 — every <c>[</c> used to scan the rest of the string for a <c>]</c>, so an
/// input with no <c>]</c> at all cost O(n²).
/// <para>
/// These guards used to assert an absolute wall-clock budget (200 KB in under
/// 500 ms), which measured the CI machine rather than the parser: on win-arm64 the
/// same three inputs took 743/767/768 ms and went red while the net11.0 leg of the
/// same job stayed green. What they assert now is what the fix actually established —
/// doubling the input doubles the cost (~2x, not ~4x) and the input costs about one
/// ordinary scan of the same bytes instead of tens of them. See
/// <see cref="ParseTiming"/> for the measurements and why they are machine
/// independent.
/// </para>
/// </summary>
[NotInParallel] // timing guards: a parallel parse would decide the ratio
public sealed class Regression_02_H2
{
    /// <summary>Size of the parse whose tree is asserted: small enough to be free, big
    /// enough that a single '[' cannot be a special case.</summary>
    private const int SampleSize = 8 * 1024;

    [Test]
    public async Task UnmatchedOpenBrackets_GrowLinearly_NotQuadratically()
    {
        await Assert.That(Markdown.Parse(new string('[', SampleSize)).Blocks).Count().IsEqualTo(1);

        var text = new string('[', ParseTiming.Small);
        var doubled = new string('[', ParseTiming.Large);

        var scaling = ParseTiming.Measure(text, doubled);

        await Assert
            .That(scaling.Ratio)
            .IsLessThan(ParseTiming.MaxGrowthOnDoubling)
            .Because(scaling.Explain("bare '[' with no ']'"));

        await Assert
            .That(scaling.WorkOfOrdinaryScans)
            .IsLessThan(ParseTiming.MaxWorkOfAnOrdinaryScan)
            .Because(scaling.Explain("bare '[' with no ']'"));

        await Assert
            .That(scaling.DoubledMs)
            .IsLessThan(ParseTiming.CatastropheCeilingMs)
            .Because("the parse must return at all, not hang");
    }

    [Test]
    public async Task UnmatchedImageBrackets_GrowLinearly_NotQuadratically()
    {
        // "![" puts one scan site every two bytes, so this family pays half as many
        // look-aheads as the bare '[' family for the same byte count
        await Assert.That(Markdown.Parse(Repeat("![", SampleSize)).Blocks).Count().IsEqualTo(1);

        var text = Repeat("![", ParseTiming.Small);
        var doubled = Repeat("![", ParseTiming.Large);

        var scaling = ParseTiming.Measure(text, doubled);

        await Assert
            .That(scaling.Ratio)
            .IsLessThan(ParseTiming.MaxGrowthOnDoubling)
            .Because(scaling.Explain("'![' with no ']'"));

        await Assert
            .That(scaling.WorkOfOrdinaryScans)
            .IsLessThan(ParseTiming.MaxWorkOfAnOrdinaryScan)
            .Because(scaling.Explain("'![' with no ']'"));

        await Assert
            .That(scaling.DoubledMs)
            .IsLessThan(ParseTiming.CatastropheCeilingMs)
            .Because("the parse must return at all, not hang");
    }

    [Test]
    public async Task OpenBracketsWithOneVeryDistantClose_GrowLinearly_NotQuadratically()
    {
        // a single ']' at the far right used to re-scan to it for every '['
        var sample = new string('[', SampleSize) + "]";
        await Assert.That(Markdown.Parse(sample).Blocks).Count().IsEqualTo(1);

        var text = new string('[', ParseTiming.Small) + "]";
        var doubled = new string('[', ParseTiming.Large) + "]";

        var scaling = ParseTiming.Measure(text, doubled);

        await Assert
            .That(scaling.Ratio)
            .IsLessThan(ParseTiming.MaxGrowthOnDoubling)
            .Because(scaling.Explain("'[' with one distant ']'"));

        await Assert
            .That(scaling.WorkOfOrdinaryScans)
            .IsLessThan(ParseTiming.MaxWorkOfAnOrdinaryScan)
            .Because(scaling.Explain("'[' with one distant ']'"));

        await Assert
            .That(scaling.DoubledMs)
            .IsLessThan(ParseTiming.CatastropheCeilingMs)
            .Because("the parse must return at all, not hang");
    }

    private static string Repeat(string unit, int size) =>
        string.Concat(Enumerable.Repeat(unit, size / unit.Length));
}
