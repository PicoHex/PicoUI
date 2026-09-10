namespace PicoTui.Screen;

public enum ColorDepth
{
    TrueColor,
    Color256,
    Color16,
    Mono,
}

public static class ColorDepthAdapter
{
    public static string SgrFor(RgbColor c, ColorDepth depth) =>
        depth switch
        {
            ColorDepth.TrueColor => $"\x1b[38;2;{c.R};{c.G};{c.B}m",
            ColorDepth.Color256 => $"\x1b[38;5;{To256(c)}m",
            ColorDepth.Mono => "",
            _ => $"\x1b[38;5;{To16(c)}m",
        };

    private static int To256(RgbColor c)
    {
        // nearest of 6x6x6 cube + 24 grays
        var r = Clamp(Math.Round(c.R / 255.0 * 5.0));
        var g = Clamp(Math.Round(c.G / 255.0 * 5.0));
        var b = Clamp(Math.Round(c.B / 255.0 * 5.0));
        return 16 + 36 * r + 6 * g + b;
    }

    private static int To16(RgbColor c)
    {
        // nearest basic color (brightness split at 128)
        var r = c.R >= 128 ? 1 : 0;
        var g = c.G >= 128 ? 1 : 0;
        var b = c.B >= 128 ? 1 : 0;
        var bright = r == 1 && g == 1 && b == 1 && (c.R > 200 || c.G > 200 || c.B > 200) ? 8 : 0;
        return bright + r * 4 + g * 2 + b;
    }

    private static int Clamp(double v) => (int)Math.Clamp(v, 0, 5);
}
