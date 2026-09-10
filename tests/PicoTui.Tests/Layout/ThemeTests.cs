namespace PicoTui.Tests.Layout;

public sealed class ThemeTests
{
    [Test]
    public async Task Dark_HasLightText()
    {
        var t = Theme.Dark();
        await Assert.That(t.Text).IsEqualTo(new RgbColor(229, 229, 229));
    }

    [Test]
    public async Task Light_HasDarkText()
    {
        var t = Theme.Light();
        await Assert.That(t.Text).IsEqualTo(new RgbColor(30, 30, 30));
    }

    [Test]
    public async Task Themes_Differ()
    {
        await Assert.That(Theme.Dark()).IsNotEqualTo(Theme.Light());
    }
}
