namespace PicoTui.Tests.Layout;

public sealed class FocusManagerTests
{
    private sealed class Fake : IComponent
    {
        public string[] Render(int width) => [];

        public void HandleInput(string seq) { }

        public void Invalidate() { }
    }

    [Test]
    public async Task SetFocus_RoutesInput()
    {
        var fm = new FocusManager();
        var a = new Fake();
        var b = new Fake();
        fm.SetFocus(a);
        fm.SetFocus(b);
        await Assert.That(fm.Focused).IsEqualTo(b);
    }
}
