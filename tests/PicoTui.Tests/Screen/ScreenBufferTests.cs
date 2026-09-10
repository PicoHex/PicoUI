namespace PicoTui.Tests.Screen;

public sealed class ScreenBufferTests
{
    [Test]
    public async Task Buffer_Resize_SetsDimensions()
    {
        var b = new ScreenBuffer();
        b.Resize(3, 5);
        await Assert.That(b.Rows).IsEqualTo(3);
        await Assert.That(b.Cols).IsEqualTo(5);
    }

    [Test]
    public async Task Buffer_SetAndGet_RoundTrips()
    {
        var b = new ScreenBuffer();
        b.Resize(2, 2);
        var cell = new Cell('X', default);
        b.Set(1, 0, cell);
        await Assert.That(b.Get(1, 0)).IsEqualTo(cell);
    }

    [Test]
    public async Task Buffer_CopyForDiff_IsIndependent()
    {
        var b = new ScreenBuffer();
        b.Resize(1, 1);
        var copy = b.CopyForDiff();
        b.Set(0, 0, new Cell('Y', default));
        await Assert.That(copy.Get(0, 0).Ch).IsEqualTo('\0');
        await Assert.That(b.Get(0, 0).Ch).IsEqualTo('Y');
    }
}
