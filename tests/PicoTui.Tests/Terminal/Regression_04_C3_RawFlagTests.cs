namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-C3 (Critical): <c>RawIflag</c> treated <c>ICANON</c>
/// (an lflag bit) as an iflag bit and never cleared <c>ICRNL</c>, so the line
/// discipline kept translating CR to LF and Enter could never be delivered.
/// Constants: Linux UAPI asm-generic/termbits.h (identical to glibc's ABI).
/// Pure bit arithmetic — runs on every platform.
/// </summary>
public sealed class Regression_04_C3_RawFlagTests
{
    // c_iflag
    private const uint Brkint = 0x2;
    private const uint Istrip = 0x20;
    private const uint Inlcr = 0x40;
    private const uint Igncr = 0x80;
    private const uint Icrnl = 0x100;
    private const uint Ixon = 0x400;
    private const uint Ixoff = 0x1000;

    // c_lflag
    private const uint Isig = 0x1;
    private const uint Icanon = 0x2;
    private const uint Echo = 0x8;
    private const uint Iexten = 0x8000;

    [Test]
    public async Task RawIflag_ClearsIcrnlFamily()
    {
        // Icrnl == 0x100 in c_iflag; leaving it set is what turns CR (0x0D)
        // into LF (0x0A), which KeyDecoder maps to Ctrl('j') instead of Enter.
        var iflag = Brkint | Istrip | Inlcr | Igncr | Icrnl | Ixon | Ixoff;
        await Assert.That(TermiosNative.RawIflag(iflag)).IsEqualTo(0u);
    }

    [Test]
    public async Task RawIflag_DoesNotUseIcanon()
    {
        // ICANON only exists in c_lflag; 0x2 in c_iflag is BRKINT. Clearing
        // BRKINT matches cfmakeraw(3), but RawLflag is the only place where
        // ICANON may be cleared — assert both directions explicitly.
        await Assert.That(TermiosNative.RawLflag(Icanon | Echo | Isig | Iexten)).IsEqualTo(0u);
        await Assert.That(TermiosNative.RawIflag(Icanon)).IsEqualTo(0u); // BRKINT, harmless
    }

    [Test]
    public async Task KeyDecoder_CrMapsToEnter()
    {
        // End-to-end for C-3: 0x0D reaching the decoder un-translated is Enter.
        var key = KeyDecoder.Decode("\r");
        await Assert.That(key.HasValue).IsTrue();
        await Assert.That(key!.Value).IsEqualTo(Key.Enter);
        await Assert.That(key.Value.Ctrl).IsFalse();
    }
}
