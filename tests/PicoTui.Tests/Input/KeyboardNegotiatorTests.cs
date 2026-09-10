namespace PicoTui.Tests.Input;

public sealed class KeyboardNegotiatorTests
{
    [Test]
    public async Task KittyResponse_Detected()
    {
        var result = KeyboardNegotiator.TryParseResponse("\x1b[?7u", out var r);
        await Assert.That(result).IsTrue();
        await Assert.That(r).IsEqualTo(NegotiationResult.Kitty);
    }

    [Test]
    public async Task ModifyOtherKeys_Enabled_Detected()
    {
        // DA response (terminal answers its features); modifyOtherKeys is enabled
        // by us, so the response is the presence of a DA at all
        var result = KeyboardNegotiator.TryParseResponse("\x1b[?1;2c", out var r);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task StartupQuery_ContainsKittyFlags()
    {
        await Assert.That(KeyboardNegotiator.StartupQuery.Contains("\x1b[>7u")).IsTrue();
    }
}
