namespace PicoHtmx.Tests;

public sealed class HtmxEventsTests
{
    [Test]
    public async Task ToastPayload_Shape()
    {
        var json = HtmxEvents.ToastPayload("Agent created", false);
        await Assert.That(json).Contains("\"toast\"");
        await Assert.That(json).Contains("\"value\"");
        await Assert.That(json).Contains("\"message\":\"Agent created\"");
        await Assert.That(json).Contains("\"error\":false");
    }

    [Test]
    public async Task ToastPayload_ErrorFlag()
    {
        var json = HtmxEvents.ToastPayload("boom", true);
        await Assert.That(json).Contains("\"error\":true");
    }

    [Test]
    public async Task EventPayload_NullUsesEmptyObject()
    {
        await Assert
            .That(HtmxEvents.EventPayload("agentCreated"))
            .IsEqualTo("{\"agentCreated\":{}}");
    }

    [Test]
    public async Task EventPayload_WithPayload_IncludesIt()
    {
        var json = HtmxEvents.EventPayload("foo", new Dictionary<string, object?> { ["x"] = 1 });
        await Assert.That(json).Contains("\"foo\"");
        await Assert.That(json).Contains("\"x\":1");
    }

    [Test]
    public async Task ToastAndEventPayload_Merges()
    {
        var json = HtmxEvents.ToastAndEventPayload("Saved", false, "agentUpdated");
        await Assert.That(json).Contains("\"toast\"");
        await Assert.That(json).Contains("\"agentUpdated\":{}");
    }
}
