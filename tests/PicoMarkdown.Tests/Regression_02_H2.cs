using System.Diagnostics;

namespace PicoMarkdown.Tests;

/// <summary>
/// 02-H2 — every <c>[</c> used to scan the rest of the string for a <c>]</c>, so an
/// input with no <c>]</c> at all cost O(n²). 200 KB of bare <c>[</c> must return
/// promptly (loose bound: this guards against quadratic blow-up, not jitter).
/// </summary>
public sealed class Regression_02_H2
{
    private const int Size = 200 * 1024;
    private const long BudgetMs = 500;

    [Test]
    public async Task UnmatchedOpenBrackets_ReturnWithinBudget()
    {
        var text = new string('[', Size);

        var sw = Stopwatch.StartNew();
        var doc = Markdown.Parse(text); // must not throw / not blow the stack
        sw.Stop();

        await Assert.That(doc.Blocks).Count().IsEqualTo(1);
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(BudgetMs);
    }

    [Test]
    public async Task UnmatchedImageBrackets_ReturnWithinBudget()
    {
        var text = string.Concat(Enumerable.Repeat("![", Size / 2));

        var sw = Stopwatch.StartNew();
        var doc = Markdown.Parse(text);
        sw.Stop();

        await Assert.That(doc.Blocks).Count().IsEqualTo(1);
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(BudgetMs);
    }

    [Test]
    public async Task OpenBracketsWithOneVeryDistantClose_ReturnWithinBudget()
    {
        // a single ']' at the far right used to re-scan to it for every '['
        var text = new string('[', Size) + "]";

        var sw = Stopwatch.StartNew();
        var doc = Markdown.Parse(text);
        sw.Stop();

        await Assert.That(doc.Blocks).Count().IsEqualTo(1);
        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(BudgetMs);
    }
}
