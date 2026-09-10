namespace PicoTui.Tests.Terminal;

public sealed class TermiosNativeTests
{
    [Test]
    public async Task RawModeFlag_ClearsIcanonEchoIsig()
    {
        // ICANON=0x2, ECHO=0x8, ISIG=0x1, IXON=0x400 (Linux termios)
        const uint icanon = 0x2,
            echo = 0x8,
            isig = 0x1,
            ixon = 0x400;
        var flags = TermiosNative.RawLflag(icanon | echo | isig);
        await Assert.That(flags & (icanon | echo | isig)).IsEqualTo(0u);
        await Assert.That(TermiosNative.RawIflag(ixon)).IsEqualTo(0u);
    }
}
