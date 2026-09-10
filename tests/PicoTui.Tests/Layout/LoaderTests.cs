namespace PicoTui.Tests.Layout;

public sealed class LoaderTests
{
    [Test]
    public async Task Advance_ChangesFrame()
    {
        var loader = new Loader();
        loader.SetActive(true);
        var first = loader.Render(10)[0];
        loader.Advance();
        var second = loader.Render(10)[0];
        await Assert.That(first).IsNotEqualTo(second);
    }

    [Test]
    public async Task Inactive_IsEmpty()
    {
        var loader = new Loader();
        await Assert.That(loader.Render(10)[0]).IsEqualTo("");
    }
}
