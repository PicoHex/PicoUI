namespace PicoTui.Tests.Terminal;

public sealed class ConsoleApiNativeTests
{
    [Test]
    public async Task VtMode_AddsOutputFlags()
    {
        // VT processing (0x4) + disable newline auto-return (0x8) — pure composition
        await Assert.That(ConsoleApiNative.WithVtMode(0u)).IsEqualTo(0x4u | 0x8u);
    }

    [Test]
    public async Task VtInputMode_AddsInputFlag()
    {
        // ENABLE_VIRTUAL_TERMINAL_INPUT (0x200) — pure composition
        await Assert.That(ConsoleApiNative.WithVtInputMode(0u)).IsEqualTo(0x200u);
    }

    [Test]
    public async Task WindowsTerminal_ConstructsWithoutConsole()
    {
        // No console in test runner — must not throw (degraded mode)
        var t = new WindowsTerminal();
        await Assert.That(t).IsNotNull();
    }
}
