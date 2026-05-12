namespace PicoTUI.Core.Tests;

public class StyleTests
{
    // ── Color ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Color_Default_HasDefaultKind()
    {
        Assert.Equal(ColorKind.Default, Color.Default.Kind);
    }

    [Fact]
    public void Color_Named_HasNamedKind()
    {
        Assert.Equal(ColorKind.Named, Color.Red.Kind);
    }

    [Fact]
    public void Color_Indexed_StoresIndex()
    {
        var c = Color.Indexed(200);
        Assert.Equal(ColorKind.Indexed, c.Kind);
        Assert.Equal(200, c.Index);
    }

    [Fact]
    public void Color_Rgb_StoresComponents()
    {
        var c = Color.Rgb(10, 20, 30);
        Assert.Equal(ColorKind.Rgb, c.Kind);
        Assert.Equal(10, c.R);
        Assert.Equal(20, c.G);
        Assert.Equal(30, c.B);
    }

    [Fact]
    public void Color_Equality()
    {
        Assert.Equal(Color.Blue, Color.Blue);
        Assert.NotEqual(Color.Blue, Color.Red);
        Assert.Equal(Color.Rgb(1, 2, 3), Color.Rgb(1, 2, 3));
        Assert.NotEqual(Color.Rgb(1, 2, 3), Color.Rgb(1, 2, 4));
        Assert.Equal(Color.Indexed(10), Color.Indexed(10));
        Assert.NotEqual(Color.Indexed(10), Color.Indexed(11));
    }

    [Fact]
    public void Color_Indexed_NotEqualToRgb()
    {
        // Indexed(0) vs Rgb(0,0,0) should not be equal
        Assert.NotEqual(Color.Indexed(0), Color.Rgb(0, 0, 0));
    }

    // ── TextAttributes ────────────────────────────────────────────────────────

    [Fact]
    public void TextAttributes_FlagsWork()
    {
        var attrs = TextAttributes.Bold | TextAttributes.Underline;
        Assert.True((attrs & TextAttributes.Bold) != 0);
        Assert.True((attrs & TextAttributes.Underline) != 0);
        Assert.False((attrs & TextAttributes.Italic) != 0);
    }

    // ── Style ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Style_Default_HasDefaultColors()
    {
        Assert.Equal(Color.Default, Style.Default.Foreground);
        Assert.Equal(Color.Default, Style.Default.Background);
        Assert.Equal(TextAttributes.None, Style.Default.Attributes);
    }

    [Fact]
    public void Style_WithForeground_ReplacesOnlyForeground()
    {
        var s = Style.Default.WithForeground(Color.Red);
        Assert.Equal(Color.Red, s.Foreground);
        Assert.Equal(Color.Default, s.Background);
    }

    [Fact]
    public void Style_WithBackground_ReplacesOnlyBackground()
    {
        var s = Style.Default.WithBackground(Color.Blue);
        Assert.Equal(Color.Default, s.Foreground);
        Assert.Equal(Color.Blue, s.Background);
    }

    [Fact]
    public void Style_AddAttributes_Combines()
    {
        var s = Style.Default.AddAttributes(TextAttributes.Bold);
        s = s.AddAttributes(TextAttributes.Italic);
        Assert.True((s.Attributes & TextAttributes.Bold) != 0);
        Assert.True((s.Attributes & TextAttributes.Italic) != 0);
    }

    [Fact]
    public void Style_RemoveAttributes()
    {
        var s = Style.Default.AddAttributes(TextAttributes.Bold | TextAttributes.Italic);
        s = s.RemoveAttributes(TextAttributes.Bold);
        Assert.False((s.Attributes & TextAttributes.Bold) != 0);
        Assert.True((s.Attributes & TextAttributes.Italic) != 0);
    }

    [Fact]
    public void Style_Equality()
    {
        var a = new Style(Color.Red, Color.Blue, TextAttributes.Bold);
        var b = new Style(Color.Red, Color.Blue, TextAttributes.Bold);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Style_Inequality()
    {
        var a = new Style(Color.Red, Color.Blue, TextAttributes.Bold);
        var b = new Style(Color.Green, Color.Blue, TextAttributes.Bold);
        Assert.NotEqual(a, b);
    }
}
