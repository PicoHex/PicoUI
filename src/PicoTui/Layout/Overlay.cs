namespace PicoTui.Layout;

public sealed record OverlayOptions(
    int? Width = null,
    int? Height = null,
    string Anchor = "center",
    bool NonCapturing = false
);

public sealed class Overlay
{
    private readonly IComponent _content;
    private readonly OverlayOptions _opts;

    public Overlay(IComponent content, OverlayOptions opts)
    {
        _content = content;
        _opts = opts;
    }

    public string[] RenderComposed(string[] viewport, int width, int height)
    {
        var content = _content.Render(width);
        var ow = content.Length > 0 ? content.Max(l => l.Length) : 0;
        var oh = content.Length;
        var (top, left) = ResolvePosition(width, height, ow, oh);
        var result = (string[])viewport.Clone();
        for (var r = 0; r < oh && top + r < height; r++)
        {
            var row = result[top + r].ToCharArray();
            for (var c = 0; c < ow && left + c < width; c++)
                if (c < content[r].Length)
                    row[left + c] = content[r][c];
            result[top + r] = new string(row);
        }
        return result;
    }

    private (int Top, int Left) ResolvePosition(int vw, int vh, int ow, int oh) =>
        _opts.Anchor switch
        {
            "bottom-right" => (Math.Max(0, vh - oh), Math.Max(0, vw - ow)),
            "top-left" => (0, 0),
            _ => (Math.Max(0, (vh - oh) / 2), Math.Max(0, (vw - ow) / 2)), // center
        };
}
