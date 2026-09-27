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
        var ow = content.Length > 0 ? content.Max(WidthTable.VisibleWidth) : 0;
        var oh = content.Length;
        var (top, left) = ResolvePosition(width, height, ow, oh);
        // pad every viewport row to the full column width so overlay writes stay in range
        var result = new string[Math.Max(height, viewport.Length)];
        for (var r = 0; r < result.Length; r++)
        {
            var line = r < viewport.Length ? viewport[r] : "";
            var cols = WidthTable.VisibleWidth(line);
            result[r] = cols >= width ? line : line + new string(' ', width - cols);
        }
        for (var r = 0; r < oh && top + r < result.Length; r++)
        {
            var insert = WidthTable.TruncateToWidth(content[r], ow);
            var insertCols = WidthTable.VisibleWidth(insert);
            result[top + r] = WidthTable.SpliceAtColumns(result[top + r], left, insert, insertCols);
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
