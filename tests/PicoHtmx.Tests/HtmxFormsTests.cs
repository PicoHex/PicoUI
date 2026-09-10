namespace PicoHtmx.Tests;

public sealed class HtmxFormsTests
{
    private static WebContext Ctx()
    {
        var req = new PicoNode.Http.HttpRequest
        {
            Method = "GET",
            Target = "/fragments/chats/1",
            Path = "/fragments/chats/1",
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        };
        return WebContext.Create(req);
    }

    [Test]
    public async Task ParseUrlEncodedBody_MultiPair_LastWins()
    {
        var dict = HtmxForms.ParseUrlEncodedBody("a=1&b=2&a=3");
        await Assert.That(dict["a"]).IsEqualTo("3");
        await Assert.That(dict["b"]).IsEqualTo("2");
    }

    [Test]
    public async Task ParseUrlEncodedBody_DecodesPlusAndPercent()
    {
        var dict = HtmxForms.ParseUrlEncodedBody("msg=hello+world&x=%2B");
        await Assert.That(dict["msg"]).IsEqualTo("hello world");
        await Assert.That(dict["x"]).IsEqualTo("+");
    }

    [Test]
    public async Task ParseUrlEncodedBody_SkipsPairsWithoutEquals()
    {
        var dict = HtmxForms.ParseUrlEncodedBody("a=1&noequals&b=2");
        await Assert.That(dict.Count).IsEqualTo(2);
    }

    [Test]
    public async Task QueryParam_ReturnsValue()
    {
        await Assert.That(HtmxForms.QueryParam("?agentId=abc&x=1", "agentId")).IsEqualTo("abc");
    }

    [Test]
    public async Task QueryParam_Missing_ReturnsNull()
    {
        await Assert.That(HtmxForms.QueryParam("?x=1", "agentId")).IsNull();
    }

    [Test]
    public async Task ParseGuid_NoRouteValue_ReturnsEmpty()
    {
        await Assert.That(HtmxForms.ParseGuid(Ctx(), "id")).IsEqualTo(Guid.Empty);
    }

    [Test]
    public async Task FormBodyTooLargeException_CarriesMaxBytes()
    {
        var ex = new FormBodyTooLargeException(1024 * 1024);
        await Assert.That(ex.MaxBytes).IsEqualTo(1024 * 1024);
        await Assert.That(ex.Message).Contains("1024KB");
    }
}
