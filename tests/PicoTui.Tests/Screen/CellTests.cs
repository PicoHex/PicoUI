namespace PicoTui.Tests.Screen;

public sealed class CellTests
{
    [Test]
    public async Task Cell_DefaultStyle_HasNoAttributes()
    {
        var c = new Cell('A', default);
        await Assert.That(c.Ch).IsEqualTo('A');
        await Assert.That(c.Style.Bold).IsFalse();
        await Assert.That(c.Style.Fg).IsNull();
    }

    [Test]
    public async Task Style_WithColor_RoundTrips()
    {
        var style = new Style { Bold = true, Fg = new RgbColor(10, 20, 30) };
        await Assert.That(style.Bold).IsTrue();
        await Assert.That(style.Fg).IsEqualTo(new RgbColor(10, 20, 30));
    }

    [Test]
    public async Task RgbColor_EqualValues_AreEqual()
    {
        await Assert.That(new RgbColor(1, 2, 3) == new RgbColor(1, 2, 3)).IsTrue();
        await Assert.That(new RgbColor(1, 2, 3) != new RgbColor(3, 2, 1)).IsTrue();
    }
}
