namespace PicoTui.Tests.Layout;

public sealed class StackLayoutTests
{
    private sealed class Fixed : IComponent
    {
        private readonly string[] _lines;

        public Fixed(params string[] lines) => _lines = lines;

        public string[] Render(int width) => _lines;

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task FixedItems_TakeNaturalHeight()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a")));
        v.Add(new LayoutItem(new Fixed("b", "c")));
        var lines = v.Render(10);
        await Assert.That(lines).Count().IsEqualTo(3);
    }

    [Test]
    public async Task GrowItem_FillsRemainingHeight()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("grow"), Grow: 1));
        v.Add(new LayoutItem(new Fixed("footer")));
        var lines = v.Render(10, 10); // availableHeight 10
        await Assert.That(lines).Count().IsEqualTo(10);
        await Assert.That(lines[0]).IsEqualTo("grow");
        await Assert.That(lines[9]).IsEqualTo("footer"); // footer pinned to bottom
    }

    [Test]
    public async Task GrowWeight_DistributesByWeight()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a"), Grow: 2));
        v.Add(new LayoutItem(new Fixed("b"), Grow: 1));
        var lines = v.Render(10, 4); // natural 2, remaining 2 → a gains more than b
        await Assert.That(lines).Count().IsEqualTo(4);
        await Assert.That(lines[0]).IsEqualTo("a");
        await Assert.That(lines[3]).IsEqualTo("b"); // lower-grow item stays below
    }

    [Test]
    public async Task Overflow_ShrinksByWeightDownToMin()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("a", "b", "c"), Shrink: 1, MinSize: 1));
        v.Add(new LayoutItem(new Fixed("x", "y"), Shrink: 0)); // pinned
        var lines = v.Render(10, 3); // natural 5, only 3 available → shrink first by 2
        await Assert.That(lines).Count().IsEqualTo(3);
        await Assert.That(lines[0]).IsEqualTo("a");
        await Assert.That(lines[2]).IsEqualTo("y"); // pinned item keeps both rows
    }

    [Test]
    public async Task InvisibleItem_Excluded()
    {
        var v = new VStack();
        v.Add(new LayoutItem(new Fixed("hidden"), Visible: (_, _) => false));
        v.Add(new LayoutItem(new Fixed("shown")));
        var lines = v.Render(10);
        await Assert.That(lines).Count().IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo("shown");
    }

    [Test]
    public async Task HStack_ConcatenatesFirstLines()
    {
        var h = new HStack();
        h.Add(new LayoutItem(new Fixed("ab")));
        h.Add(new LayoutItem(new Fixed("cd")));
        var lines = h.Render(10);
        await Assert.That(lines).Count().IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo("abcd");
    }

    [Test]
    public async Task HStack_TruncatesByVisibleWidth()
    {
        var h = new HStack();
        h.Add(new LayoutItem(new Fixed("中"))); // CJK = 2 columns
        h.Add(new LayoutItem(new Fixed("ab")));
        var lines = h.Render(3); // only 3 columns: the CJK char takes 2 cols + a(1) fits; b must be cut
        await Assert.That(lines).Count().IsEqualTo(1);
        await Assert.That(lines[0]).IsEqualTo("中a");
    }
}
