namespace PicoTui.Tests.Layout;

public sealed class StatusBarTests
{
    [Test]
    public async Task Text_TruncatedToWidth()
    {
        var sb = new StatusBar();
        sb.Set("a long status message here");
        var line = sb.Render(10)[0];
        await Assert.That(line.Length).IsLessThanOrEqualTo(10);
    }

    [Test]
    public async Task State_RightAligned()
    {
        var sb = new StatusBar();
        sb.Set("left");
        sb.SetState("IDLE");
        var line = sb.Render(20)[0];
        await Assert.That(line.Contains("left")).IsTrue();
        await Assert.That(line.Contains("IDLE")).IsTrue();
    }
}
