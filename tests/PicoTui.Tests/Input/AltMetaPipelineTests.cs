namespace PicoTui.Tests.Input;

public sealed class AltMetaPipelineTests
{
    [Test]
    public async Task EscX_ThroughBuffer_ThenDecoder_IsAltX()
    {
        var buffer = new StdinBuffer();
        buffer.Append(Encoding.ASCII.GetBytes("\x1bx"));
        var unit = buffer.Drain().Single();

        var key = KeyDecoder.Decode(unit);
        await Assert.That(key).IsNotEqualTo(null);
        await Assert.That(key!.Value.Char).IsEqualTo('x');
        await Assert.That(key.Value.Alt).IsTrue();
    }
}
