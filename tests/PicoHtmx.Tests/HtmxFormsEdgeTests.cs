namespace PicoHtmx.Tests;

/// <summary>
/// Query parsing must match whole keys (not substrings) and decode like the
/// body path (<c>+</c> means space).
/// </summary>
public sealed class HtmxFormsEdgeTests
{
    [Test]
    public async Task QueryParam_KeySuffix_DoesNotMatch()
    {
        await Assert.That(HtmxForms.QueryParam("?xfoo=1&foo=2", "foo")).IsEqualTo("2");
    }

    [Test]
    public async Task QueryParam_KeyInsideValue_DoesNotMatch()
    {
        await Assert.That(HtmxForms.QueryParam("?basepath=x&path=y", "path")).IsEqualTo("y");
    }

    [Test]
    public async Task QueryParam_DecodesPlusAsSpace()
    {
        await Assert.That(HtmxForms.QueryParam("?plus=a+b", "plus")).IsEqualTo("a b");
    }

    [Test]
    public async Task QueryParam_Missing_ReturnsNull()
    {
        await Assert.That(HtmxForms.QueryParam("?a=1", "b")).IsNull();
    }
}

/// <summary>ListPage must enumerate its source once (single-use sequences are legal).</summary>
public sealed class ComponentsEnumerationTests
{
    private sealed class SingleUseSequence : IEnumerable<string>
    {
        private bool _used;

        public IEnumerator<string> GetEnumerator()
        {
            if (_used)
                throw new InvalidOperationException("enumerated twice");
            _used = true;
            yield return "a";
            yield return "b";
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }

    [Test]
    public async Task ListPage_SingleUseSequence_DoesNotThrow()
    {
        var html = Ux.ListPage("Test", new SingleUseSequence(), s => H.P(s));

        await Assert.That(html).Contains("<p>a</p>");
        await Assert.That(html).Contains("<p>b</p>");
    }
}
