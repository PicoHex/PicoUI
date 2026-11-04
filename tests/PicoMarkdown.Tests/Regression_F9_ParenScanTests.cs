using System.Diagnostics;

namespace PicoMarkdown.Tests;

/// <summary>
/// F9-3 guard (02-H2 residual): the fix linearised the <c>]</c> scan with an
/// incremental cache but left <c>TryParseLinkish</c>'s
/// <c>text.IndexOf(')', close + 2)</c> unbounded. <c>"[a]("</c> is the cheapest
/// way to make the parser re-scan the whole tail for a <c>)</c> that is never
/// there: 205 KB ⇒ 279 ms, 819 KB ⇒ 2.3 s, 1 MB ⇒ 5.8 s (pure quadratic).
/// <para>
/// The guards below measure the same two families the reviewer used — no
/// closing paren at all, and one paren at the very end — and additionally pin
/// the <em>shape</em> of the curve (1× vs 2× input) so a constant-factor tweak
/// cannot pass as a fix.
/// </para>
/// </summary>
public sealed class Regression_F9_ParenScanTests
{
    /// <summary>800 KB: the size that took ~2.3 s before the fix.</summary>
    private const int Large = 204800;

    private const int Small = 102400;

    private const long BudgetMs = 500;

    /// <summary>Timing floor: sub-20 ms runs are dominated by start-up jitter, so
    /// the ratio guard compares against at least this value. A quadratic path
    /// (8× the linear cost at 2× the size) is far above the floor.</summary>
    private const double NoiseFloorMs = 20;

    private static string Repeat(int count) => string.Concat(Enumerable.Repeat("[a](", count));

    /// <summary>Parses and returns the best (smallest) elapsed ms of three runs —
    /// a slow first iteration (JIT, tiering) must not decide the guard.</summary>
    private static double Measure(string text)
    {
        var best = double.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            var doc = Markdown.Parse(text);
            sw.Stop();
            if (doc.Blocks.Count == 0)
                throw new InvalidOperationException("expected one paragraph block");
            best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
        }
        return best;
    }

    [Test]
    public async Task LinkTextFollowedByOpenParen_NoClosingParen_StaysWithinBudget()
    {
        var text = Repeat(Large); // 800 KB of "[a](" with no ')' anywhere

        var ms = Measure(text);

        await Assert
            .That(ms < BudgetMs)
            .IsTrue()
            .Because($"819 KB of \"[a](\" took {ms:F0} ms (budget {BudgetMs} ms): the ')' scan is unbounded");
    }

    [Test]
    public async Task LinkTextFollowedByOpenParen_OneDistantClosingParen_StaysWithinBudget()
    {
        var text = Repeat(Large) + ")";

        var ms = Measure(text);

        await Assert
            .That(ms < BudgetMs)
            .IsTrue()
            .Because($"819 KB of \"[a](\" + \")\" took {ms:F0} ms (budget {BudgetMs} ms)");
    }

    [Test]
    public async Task DoublingTheInput_DoesNotQuadrupleTheCost()
    {
        // warm up both shapes so JIT does not land in the measurement
        _ = Measure(Repeat(1024));
        _ = Measure(Repeat(1024) + ")");

        var small = Math.Max(Measure(Repeat(Small)), NoiseFloorMs);
        var large = Math.Max(Measure(Repeat(Large)), NoiseFloorMs);
        var ratio = large / small;

        await Assert
            .That(ratio < 3.0)
            .IsTrue()
            .Because(
                $"2x the input cost {ratio:F2}x the time ({small:F0} ms → {large:F0} ms): linear must stay below 3x, quadratic is ~4x"
            );
    }

    [Test]
    public async Task DoublingTheInput_WithOneDistantClosingParen_DoesNotQuadrupleTheCost()
    {
        _ = Measure(Repeat(1024) + ")");

        var small = Math.Max(Measure(Repeat(Small) + ")"), NoiseFloorMs);
        var large = Math.Max(Measure(Repeat(Large) + ")"), NoiseFloorMs);
        var ratio = large / small;

        await Assert
            .That(ratio < 3.0)
            .IsTrue()
            .Because(
                $"2x the input cost {ratio:F2}x the time ({small:F0} ms → {large:F0} ms) on the '(...)' path"
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
