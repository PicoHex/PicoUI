namespace PicoTui.Tests.Input;

public sealed class KeyDecoderTests
{
    [Test]
    public async Task ControlChar_CtrlA()
    {
        var k = KeyDecoder.Decode("\x01");
        await Assert.That(k).IsNotEqualTo(null);
        await Assert.That(k!.Value.Char).IsEqualTo('a');
        await Assert.That(k.Value.Ctrl).IsTrue();
    }

    [Test]
    public async Task Enter_Decodes()
    {
        var k = KeyDecoder.Decode("\r");
        await Assert.That(k).IsEqualTo(Key.Enter);
    }

    [Test]
    public async Task UpArrow_Plain()
    {
        var k = KeyDecoder.Decode("\x1b[A");
        await Assert.That(k).IsEqualTo(Key.ArrowUp);
    }

    [Test]
    public async Task CtrlUpArrow_CsiModifier()
    {
        var k = KeyDecoder.Decode("\x1b[1;5A");
        await Assert.That(k).IsNotEqualTo(null);
        await Assert.That(k!.Value.IsArrowUp).IsTrue();
        await Assert.That(k.Value.Ctrl).IsTrue();
    }

    [Test]
    public async Task AltX_LegacyMeta()
    {
        var k = KeyDecoder.Decode("\x1bx");
        await Assert.That(k).IsNotEqualTo(null);
        await Assert.That(k!.Value.Char).IsEqualTo('x');
        await Assert.That(k.Value.Alt).IsTrue();
    }

    [Test]
    public async Task KittySequence_Modifiers()
    {
        // kitty: ESC [ 97;5u = ctrl+a
        var k = KeyDecoder.Decode("\x1b[97;5u");
        await Assert.That(k).IsNotEqualTo(null);
        await Assert.That(k!.Value.Char).IsEqualTo('a');
        await Assert.That(k.Value.Ctrl).IsTrue();
    }
}
