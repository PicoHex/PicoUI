namespace PicoTui.Tests.Terminal;

public sealed class VirtualTerminalTests
{
    [Test]
    public async Task Write_RecordsOutput()
    {
        var vt = new VirtualTerminal(80, 24);
        vt.Write("hello");
        await Assert.That(vt.Output).IsEqualTo("hello");
    }

    [Test]
    public async Task ScriptInput_DeliversToHandler()
    {
        var vt = new VirtualTerminal(80, 24);
        string? received = null;
        vt.Start(s => received = s, () => { });
        vt.InjectInput("abc");
        await Assert.That(received).IsEqualTo("abc");
    }

    [Test]
    public async Task Resize_NotifiesHandler()
    {
        var vt = new VirtualTerminal(80, 24);
        var resized = false;
        vt.Start(_ => { }, () => resized = true);
        vt.Resize(120, 40);
        await Assert.That(resized).IsTrue();
        await Assert.That(vt.Columns).IsEqualTo(120);
        await Assert.That(vt.Rows).IsEqualTo(40);
    }
}
