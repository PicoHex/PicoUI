namespace PicoTui.Tests.Input;

/// <summary>
/// ESC disambiguation must not swallow already-buffered input: the flush
/// returns the first unit and the remaining units stay available.
/// </summary>
public sealed class StdinBufferFlushTests
{
    [Test]
    public async Task FlushEscAsync_KeepsRemainingUnitsBuffered()
    {
        var buffer = new StdinBuffer();
        buffer.Append("ab"u8);

        var first = await buffer.FlushEscAsync(TimeSpan.FromMilliseconds(20));

        await Assert.That(first).IsEqualTo("a");
        await Assert.That(buffer.Drain().ToArray()).IsEquivalentTo(new[] { "b" });
    }
}
