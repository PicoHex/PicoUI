namespace PicoTui.Tests.Screen;

public sealed class ColorDepthTests
{
    [Test]
    public async Task TrueColor_EmitsRgb()
    {
        var s = ColorDepthAdapter.SgrFor(new RgbColor(10, 20, 30), ColorDepth.TrueColor);
        await Assert.That(s).IsEqualTo("\x1b[38;2;10;20;30m");
    }

    [Test]
    public async Task Color256_MapsToCubeIndex()
    {
        var s = ColorDepthAdapter.SgrFor(new RgbColor(0, 0, 0), ColorDepth.Color256);
        await Assert.That(s).IsEqualTo("\x1b[38;5;16m"); // black = cube index 16
    }

    [Test]
    public async Task Mono_EmitsNothing()
    {
        var s = ColorDepthAdapter.SgrFor(new RgbColor(10, 20, 30), ColorDepth.Mono);
        await Assert.That(s).IsEqualTo("");
    }
}
