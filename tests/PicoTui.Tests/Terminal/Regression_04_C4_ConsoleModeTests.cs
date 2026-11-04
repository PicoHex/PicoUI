namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-C4 (Critical): <c>WithVtInputMode</c> only OR-ed
/// <c>ENABLE_VIRTUAL_TERMINAL_INPUT</c> in — <c>ENABLE_LINE_INPUT</c> (0x2) and
/// <c>ENABLE_ECHO_INPUT</c> (0x4) were never cleared, so Windows delivered
/// input only after Enter and echoed it a second time.
/// Pure flag composition — runs on every platform.
/// </summary>
public sealed class Regression_04_C4_ConsoleModeTests
{
    private const uint EnableProcessedInput = 0x1;
    private const uint EnableLineInput = 0x2;
    private const uint EnableEchoInput = 0x4;
    private const uint EnableVirtualTerminalInput = 0x200;

    [Test]
    public async Task WithVtInputMode_ClearsLineAndEchoInput()
    {
        var mode = ConsoleApiNative.WithVtInputMode(
            EnableProcessedInput | EnableLineInput | EnableEchoInput
        );
        await Assert.That(mode & EnableLineInput).IsEqualTo(0u);
        await Assert.That(mode & EnableEchoInput).IsEqualTo(0u);
        await Assert.That(mode & EnableVirtualTerminalInput).IsEqualTo(EnableVirtualTerminalInput);
        // Ctrl-C delivery (ENABLE_PROCESSED_INPUT) must survive.
        await Assert.That(mode & EnableProcessedInput).IsEqualTo(EnableProcessedInput);
    }

    [Test]
    public async Task WithVtInputMode_IsIdempotent()
    {
        var once = ConsoleApiNative.WithVtInputMode(EnableLineInput | EnableEchoInput);
        var twice = ConsoleApiNative.WithVtInputMode(once);
        await Assert.That(twice).IsEqualTo(once);
        await Assert.That(once).IsEqualTo(EnableVirtualTerminalInput);
    }
}
