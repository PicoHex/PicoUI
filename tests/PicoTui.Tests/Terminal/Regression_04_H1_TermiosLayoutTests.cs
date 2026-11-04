namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-H1 (High): the managed <c>Termios</c> must match Linux's
/// <c>sizeof(struct termios) == 60</c> (both glibc and musl append
/// <c>speed_t c_ispeed, c_ospeed</c> after <c>c_cc[NCCS=32]</c>), otherwise
/// <c>tcgetattr</c> writes 8 bytes past the buffer.
/// The managed layout is OS-independent, so this runs on every runner.
/// </summary>
public sealed class Regression_04_H1_TermiosLayoutTests
{
    [Test]
    public async Task Termios_HasSpeedFieldsAtTheLinuxOffsets()
    {
        var type = typeof(Termios);
        var line = System.Runtime.InteropServices.Marshal.OffsetOf(type, "c_line").ToInt32();
        var cc = System.Runtime.InteropServices.Marshal.OffsetOf(type, "c_cc").ToInt32();
        var ispeed = System.Runtime.InteropServices.Marshal.OffsetOf(type, "c_ispeed").ToInt32();
        var ospeed = System.Runtime.InteropServices.Marshal.OffsetOf(type, "c_ospeed").ToInt32();

        await Assert.That(line).IsEqualTo(16);
        await Assert.That(cc).IsEqualTo(17);
        await Assert.That(ispeed).IsEqualTo(52);
        await Assert.That(ospeed).IsEqualTo(56);
    }

    [Test]
    public async Task Termios_LayoutAssertion_AgreesWithTheMeasuredSize()
    {
        // Debug.Assert path (compiled out in Release) plus the const the guard uses.
        var expected = TermiosNative.LinuxTermiosSize;
        await Assert.That(TermiosNative.LayoutIsValid).IsTrue();
        await Assert.That(expected).IsEqualTo(60);
        TermiosNative.AssertLayout(); // no-op unless DEBUG; must never throw
    }
}
