namespace PicoTui.Tests.Terminal;

public sealed class TerminalFactoryTests
{
    [Test]
    public async Task Create_ReturnsTerminal()
    {
        var t = TerminalFactory.Create();
        await Assert.That(t.Columns).IsGreaterThan(0);
        await Assert.That(t.Rows).IsGreaterThan(0);
    }
}
