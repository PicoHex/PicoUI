namespace PicoTui.Layout;

public sealed record Theme(
    RgbColor Border,
    RgbColor Text,
    RgbColor Accent,
    RgbColor Muted,
    RgbColor Warning,
    RgbColor Error,
    RgbColor Selection
)
{
    public static Theme Dark() =>
        new(
            new RgbColor(80, 80, 80),
            new RgbColor(229, 229, 229),
            new RgbColor(86, 156, 214),
            new RgbColor(120, 120, 120),
            new RgbColor(220, 180, 60),
            new RgbColor(230, 80, 80),
            new RgbColor(60, 100, 180)
        );

    public static Theme Light() =>
        new(
            new RgbColor(120, 120, 120),
            new RgbColor(30, 30, 30),
            new RgbColor(0, 90, 180),
            new RgbColor(140, 140, 140),
            new RgbColor(150, 110, 20),
            new RgbColor(190, 40, 40),
            new RgbColor(160, 190, 240)
        );
}
