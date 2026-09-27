namespace PicoTui.Tests.Input;

/// <summary>
/// Decoded input arrives from terminals (Kitty/modifyOtherKeys/xterm emit
/// colon-separated sub-parameters). Unknown shapes must not throw, and every
/// well-formed key sequence must decode (previously plain CSI-u keys were
/// silently dropped and sub-parameters crashed with FormatException).
/// </summary>
public sealed class KeyDecoderRobustnessTests
{
    [Test]
    public async Task KittySubParameters_DecodeWithoutThrowing()
    {
        var k = KeyDecoder.Decode("\x1b[97;5:3u");

        await Assert.That(k).IsNotEqualTo(null);
        await Assert.That(k!.Value.Char).IsEqualTo('a');
        await Assert.That(k.Value.Ctrl).IsTrue();
    }

    [Test]
    public async Task KittyPlainKey_Decodes()
    {
        var k = KeyDecoder.Decode("\x1b[97u");

        await Assert.That(k).IsNotEqualTo(null);
        await Assert.That(k!.Value.Char).IsEqualTo('a');
    }

    [Test]
    public async Task MalformedNumericParameter_ReturnsNull()
    {
        await Assert.That(KeyDecoder.Decode("\x1b[1;xu")).IsNull();
        await Assert.That(KeyDecoder.Decode("\x1b[x;1u")).IsNull();
    }

    [Test]
    public async Task OverflowingParameter_ReturnsNull()
    {
        await Assert.That(KeyDecoder.Decode("\x1b[1;99999999999999999999u")).IsNull();
    }

    [Test]
    public async Task CodePointBeyondChar_ReturnsNull()
    {
        await Assert.That(KeyDecoder.Decode("\x1b[99999;5u")).IsNull();
    }
}
