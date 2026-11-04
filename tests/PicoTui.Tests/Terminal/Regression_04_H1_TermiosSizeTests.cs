namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-H1 (High): the managed <c>Termios</c> struct was 52 bytes
/// while both glibc and musl declare <c>sizeof(struct termios) == 60</c>
/// (they append <c>speed_t c_ispeed, c_ospeed</c> after <c>c_cc[NCCS=32]</c>).
/// <c>tcgetattr</c> therefore wrote 8 bytes past the managed buffer.
/// The struct layout is a managed sequential layout, so its size is identical
/// on every OS — this test is meaningful on any runner, not just Linux.
/// </summary>
public sealed class Regression_04_H1_TermiosSizeTests
{
    [Test]
    public async Task Termios_IsSixtyBytes()
    {
        var size = System.Runtime.CompilerServices.Unsafe.SizeOf<Termios>();
        await Assert.That(size).IsEqualTo(60);
    }
}
