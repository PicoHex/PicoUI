namespace PicoTui.Tests.Layout;

/// <summary>
/// 05-H1 guard: the grow pass must compute each item's share from the FROZEN
/// remaining total (not from a running remainder that has already been
/// decremented), and the rounding leftover must be handed out by a documented
/// rule instead of dropping the whole remainder on the first grow item.
/// Equal weights must therefore split the available height evenly.
/// </summary>
public sealed class Regression_05_H1_StackAllocationTests
{
    private sealed class Fixed(params string[] lines) : IComponent
    {
        public string[] Render(int width) => lines;

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task EqualGrow_AtHeight40_SplitsTwentyTwenty()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a"), Grow: 1));
        v.Add(new LayoutItem(new Fixed("b"), Grow: 1));

        var lines = v.Render(40, 40);

        await Assert.That(lines.Length).IsEqualTo(40);
        // item 0 owns rows 0..19 ("a" + 19 blanks), item 1 owns rows 20..39
        await Assert.That(lines[19]).IsEqualTo("");
        await Assert.That(lines[20]).IsEqualTo("b");
    }

    [Test]
    public async Task EqualGrow_AtHeight10_SplitsFiveFive()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a"), Grow: 1));
        v.Add(new LayoutItem(new Fixed("b"), Grow: 1));

        var lines = v.Render(40, 10);

        await Assert.That(lines.Length).IsEqualTo(10);
        await Assert.That(lines[4]).IsEqualTo("");
        await Assert.That(lines[5]).IsEqualTo("b");
    }

    [Test]
    public async Task WeightedGrow_ThreeToOne_SplitsByWeight()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a"), Grow: 3));
        v.Add(new LayoutItem(new Fixed("b"), Grow: 1));

        // natural 2, available 6 → remaining 4 → 3:1 → heights 4 and 2
        var lines = v.Render(40, 6);

        await Assert.That(lines.Length).IsEqualTo(6);
        await Assert.That(lines[3]).IsEqualTo("");
        await Assert.That(lines[4]).IsEqualTo("b");
    }

    [Test]
    public async Task OddLeftover_GoesToTheHeaviestThenFirstItem()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a"), Grow: 1));
        v.Add(new LayoutItem(new Fixed("b"), Grow: 1));

        // natural 2, available 5 → remaining 3 → floor 1 each, 1 row left over →
        // documented rule: hand residue out in (grow desc, index asc) order
        var lines = v.Render(40, 5);

        await Assert.That(lines.Length).IsEqualTo(5);
        await Assert.That(lines[2]).IsEqualTo(""); // item 0 grew to 3 rows
        await Assert.That(lines[3]).IsEqualTo("b"); // item 1 got 2 rows
    }

    [Test]
    public async Task GrowIsBoundedByMaxSize_AndTheResidueMovesToTheNextItem()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a"), Grow: 1, MaxSize: 3));
        v.Add(new LayoutItem(new Fixed("b"), Grow: 1));

        // available 10, natural 2 → remaining 8 → ideal 4/4, a is capped at 3
        var lines = v.Render(40, 10);

        await Assert.That(lines.Length).IsEqualTo(10);
        await Assert.That(lines[2]).IsEqualTo(""); // a: rows 0..2
        await Assert.That(lines[3]).IsEqualTo("b"); // b: rows 3..9 (7 rows)
    }
}
