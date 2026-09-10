namespace PicoTui.Tests.Input;

public sealed class SgrMouseTests
{
    [Test]
    public async Task WheelUp_Parsed()
    {
        var ok = SgrMouse.TryParse("\x1b[<64;10;5M", out var evt);
        await Assert.That(ok).IsTrue();
        await Assert.That(evt.WheelUp).IsTrue();
        await Assert.That(evt.X).IsEqualTo(10);
        await Assert.That(evt.Y).IsEqualTo(5);
    }

    [Test]
    public async Task EnableSeq_IsSgrMode()
    {
        var enable = SgrMouse.EnableSeq; // const → local var (TUnit rejects const assertions)
        await Assert.That(enable).IsEqualTo("\x1b[?1000;1006h");
    }
}
