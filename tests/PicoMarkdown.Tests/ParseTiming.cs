using System.Diagnostics;

namespace PicoMarkdown.Tests;

/// <summary>
/// The yardstick the PicoMarkdown complexity guards measure with.
/// <para>
/// An absolute WALL-CLOCK BUDGET ("200 KB must parse in 500 ms") is not a property of
/// the parser — it is a property of the machine that ran it. On the win-arm64 CI leg
/// the same 200 KB input that costs 2.7 ms on a desktop measured 743-768 ms, so a
/// 500 ms budget went red while the algorithm was perfectly linear (the net11.0 leg
/// of the very same job passed). A budget cannot tell "slow runner" from
/// "quadratic parser"; a comparison can.
/// </para>
/// <para>
/// So every guard here measures the same algorithm twice over and asserts on the
/// RELATION, never on a clock reading. Two independent relations, because one alone
/// is not enough (both were checked against a mutation that removes the <c>]</c> /
/// <c>)</c> memoisation):
/// </para>
/// <list type="number">
/// <item>
/// <see cref="Scaling.Ratio"/> — cost at 2N over cost at N. Linear ≈ 2x, quadratic
/// ≈ 4x. This is the shape contract, and it is machine independent: a runner that is
/// 20x slower multiplies both sides by 20.
/// </item>
/// <item>
/// <see cref="Scaling.WorkOfOrdinaryScans"/> — cost of the pathological input over
/// the cost of the SAME NUMBER OF BYTES of ordinary text, parsed in the same process,
/// moments earlier. Ordinary text is one pass by definition, so this says "how many
/// passes over the input this input costs". <em>This is the assertion that bites.</em>
/// The 2x-ratio alone did not: with the memoisation removed, the pathological tail
/// re-scan is cheap enough per byte to be hidden by cache at these sizes, so the
/// measured ratio under mutation fell to 2.12x, 2.39x and 2.51x on three separate
/// runs — green runs, on a parser that re-scans the whole tail for every '['. The
/// same runs measured 71x-404x more work than an ordinary scan of the same size,
/// while the memoised parser measures 1.1x-2.5x: a separation wide enough that
/// neither a fast desktop nor a slow arm64 runner can cross it.
/// </item>
/// </list>
/// <para>
/// Every measurement is the MEDIAN of three timed runs after one untimed warm-up run
/// (JIT, tiering and first touch must not decide). Supporting numbers:
/// <see cref="NoiseFloorMs"/> keeps sub-millisecond jitter from deciding
/// <see cref="Scaling.Ratio"/> on a fast desktop, <see cref="OrdinaryScanFloorMs"/>
/// does the same for the denominator of <see cref="Scaling.WorkOfOrdinaryScans"/>,
/// and <see cref="CatastropheCeilingMs"/> is a hang detector — the only absolute
/// number left, and deliberately far too loose to be a performance verdict.
/// </para>
/// </summary>
internal static class ParseTiming
{
    /// <summary>The input sizes every guard compares: 50 KB and 100 KB.</summary>
    internal const int Small = 50 * 1024;

    /// <summary>The doubled input, <see cref="Small"/> x 2.</summary>
    internal const int Large = 2 * Small;

    /// <summary>Linear is ≈ 2.0 and quadratic is ≈ 4.0, so 3.0 splits them with room
    /// for jitter on both sides. Memoised parser here: 1.4-2.5 measured.</summary>
    internal const double MaxGrowthOnDoubling = 3.0;

    /// <summary>How much more than one ordinary scan of the same bytes a pathological
    /// input may cost. Memoised parser: 1.1-2.5 measured. Memoisation removed:
    /// 71-404 measured — an order of magnitude above the line, so the verdict
    /// survives both a fast desktop and a slow CI runner.</summary>
    internal const double MaxWorkOfAnOrdinaryScan = 10.0;

    /// <summary>Runs below this are dominated by timer jitter, so both sides of
    /// <see cref="Scaling.Ratio"/> are clamped up to it. On a fast machine 50 KB
    /// parses in ~1 ms and 100 KB in ~2 ms — both clamp, and the ratio falls back to
    /// 1.0 instead of being decided by noise. Clamping can only make the ratio
    /// SMALLER, so for a linear parser (true ratio 2) the floor can never produce a
    /// false failure; and it cannot hide a quadratic one either, which needs
    /// 60-400 ms at these sizes — 3x to 20x the floor.</summary>
    internal const double NoiseFloorMs = 20.0;

    /// <summary>An ordinary scan of 100 KB takes under a millisecond on any machine
    /// that can run this suite at all; clamping the denominator up to 1 ms only makes
    /// <see cref="Scaling.WorkOfOrdinaryScans"/> smaller, i.e. it can never turn a
    /// green run red on a slow runner.</summary>
    internal const double OrdinaryScanFloorMs = 1.0;

    /// <summary>Hang detector, NOT a budget: it catches "the parse never returns",
    /// which no ratio can. 10 s is far above the linear cost of the largest input
    /// these guards use, so no machine speed chooses the verdict here.</summary>
    internal const long CatastropheCeilingMs = 10_000;

    private const int WarmupRuns = 1;
    private const int MeasuredRuns = 3;

    /// <summary>
    /// Median wall-clock cost of one <see cref="Markdown.Parse"/> of
    /// <paramref name="text"/>, after an untimed warm-up run. Median (not best, not
    /// mean): one descheduled run must not decide, and neither should a lucky one.
    /// </summary>
    internal static double MedianMs(string text)
    {
        for (var i = 0; i < WarmupRuns; i++)
        {
            _ = ParseChecked(text);
        }

        var runs = new double[MeasuredRuns];
        for (var i = 0; i < runs.Length; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = ParseChecked(text);
            sw.Stop();
            runs[i] = sw.Elapsed.TotalMilliseconds;
        }

        Array.Sort(runs);
        return runs[runs.Length / 2];
    }

    /// <summary>
    /// Measures <paramref name="text"/> (N), <paramref name="doubled"/> (2N) and, for
    /// each of them, an ordinary text of the same byte count — the one-pass baseline
    /// the pathological inputs are judged against.
    /// </summary>
    internal static Scaling Measure(string text, string doubled) =>
        new(
            MedianMs(text),
            MedianMs(doubled),
            MedianMs(Ordinary(text.Length)),
            MedianMs(Ordinary(doubled.Length))
        );

    /// <summary>
    /// "The same number of bytes, with nothing to look ahead for." Plain lowercase
    /// text is one paragraph and one pass through the parser, so it prices what a
    /// linear scan of this input size costs on THIS machine, right now.
    /// </summary>
    internal static string Ordinary(int size) => new('a', size);

    /// <summary>
    /// A parse that returns no block is not a fast parse — it is a parse that gave
    /// up. Failing here keeps "fast" from meaning "did nothing".
    /// </summary>
    private static IReadOnlyList<BlockNode> ParseChecked(string text)
    {
        var doc = Markdown.Parse(text);
        if (doc.Blocks.Count == 0)
        {
            throw new InvalidOperationException("expected at least one block");
        }

        return doc.Blocks;
    }
}

/// <summary>
/// One measurement: what N cost, what 2N cost, and what the same byte counts cost as
/// ordinary text. <see cref="Ratio"/> and <see cref="WorkOfOrdinaryScans"/> are the
/// machine-independent numbers the guards assert on.
/// </summary>
internal readonly record struct Scaling(
    double SmallMs,
    double DoubledMs,
    double OrdinaryMs,
    double OrdinaryDoubledMs
)
{
    /// <summary>Cost of 2N over cost of N, both clamped up to
    /// <see cref="ParseTiming.NoiseFloorMs"/>. Linear ≈ 2, quadratic ≈ 4.</summary>
    internal double Ratio =>
        Math.Max(DoubledMs, ParseTiming.NoiseFloorMs) / Math.Max(SmallMs, ParseTiming.NoiseFloorMs);

    /// <summary>Cost of 2N in units of "one ordinary scan of 2N bytes". A parser that
    /// re-scans the tail for every <c>[</c> or <c>(</c> cannot stay near 1.</summary>
    internal double WorkOfOrdinaryScans =>
        DoubledMs / Math.Max(OrdinaryDoubledMs, ParseTiming.OrdinaryScanFloorMs);

    /// <summary>Every number that decided the verdict, so a failure explains itself on
    /// a machine nobody can log into.</summary>
    internal string Explain(string shape) =>
        $"{shape}: {ParseTiming.Small / 1024} KB took {SmallMs:F2} ms and "
        + $"{ParseTiming.Large / 1024} KB took {DoubledMs:F2} ms → {Ratio:F2}x for 2x the input "
        + $"(linear ≈ 2x, quadratic ≈ 4x, floor {ParseTiming.NoiseFloorMs:F0} ms); "
        + $"the same {ParseTiming.Large / 1024} KB of ordinary text took {OrdinaryDoubledMs:F2} ms "
        + $"→ {WorkOfOrdinaryScans:F1}x an ordinary scan "
        + $"(limit {ParseTiming.MaxWorkOfAnOrdinaryScan:F0}x)";
}
