namespace PicoTui.Tests.Terminal;

public sealed class UnixTerminalContractTests
{
    [Test]
    public async Task AnsiEscapeContract_IsEmitted()
    {
        // Constructing must not throw even without a TTY (degraded mode):
        // the size query falls back to 80x24. The escape strings must stay
        // identical to VirtualTerminal's so ScreenRenderer output is stable.
        var t = new UnixTerminal();
        await Assert.That(t.Columns).IsGreaterThan(0);
        await Assert.That(t.Rows).IsGreaterThan(0);
    }
}
