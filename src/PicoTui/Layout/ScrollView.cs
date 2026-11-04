namespace PicoTui.Layout;

/// <summary>Scroll view options.</summary>
/// <param name="FollowEnd">Start out pinned to the end of the content. A manual
/// scroll up detaches the pin (<see cref="ScrollView.FollowEnd"/>); scrolling
/// back to the bottom re-arms it.</param>
/// <param name="Primary">Reserved (not wired yet): capture wheel events over
/// non-scroll regions.</param>
/// <param name="Overscroll">Reserved (not wired yet): chain overscroll to the parent.</param>
public sealed record ScrollOptions(
    bool FollowEnd = false,
    bool Primary = false,
    string Overscroll = "none"
);

public sealed class ScrollView : IComponent
{
    private readonly IComponent _child;
    private int _offset;
    private bool _followEnd;

    public ScrollView(IComponent child, ScrollOptions opts)
    {
        _child = child;
        _followEnd = opts.FollowEnd;
    }

    /// <summary>Current top line of the viewport.</summary>
    public int Offset => _offset;

    /// <summary>True while the viewport is pinned to the end of the content.
    /// 05-H2: a manual scroll up detaches it, reaching the bottom re-arms it.</summary>
    public bool FollowEnd => _followEnd;

    /// <summary>Scrolls by <paramref name="n"/> lines (negative = up).</summary>
    /// <remarks>A negative scroll is an explicit user action, so it detaches the
    /// follow-end pin — otherwise the next frame would snap straight back to the
    /// bottom and history could never be read. The bottom is re-detected on the
    /// next viewport render.</remarks>
    public void ScrollBy(int n)
    {
        if (n < 0)
            _followEnd = false;
        _offset = Math.Max(0, _offset + n);
    }

    /// <summary>Jumps to the last page and re-arms the follow-end pin.</summary>
    public void ScrollToEnd(int contentHeight, int viewportHeight)
    {
        _offset = Math.Max(0, contentHeight - viewportHeight);
        _followEnd = true;
    }

    public string[] RenderViewport(int width, int viewportHeight)
    {
        var content = _child.Render(width);
        var bounded = viewportHeight != int.MaxValue;

        if (bounded)
        {
            var maxOffset = Math.Max(0, content.Length - viewportHeight);
            if (_followEnd)
                _offset = maxOffset;
            else
            {
                _offset = Math.Min(_offset, maxOffset);
                if (_offset >= maxOffset)
                    _followEnd = true; // back at the bottom → follow the end again
            }
        }
        else
        {
            // unbounded (natural-height measurement): keep at least one line visible
            _offset = Math.Min(_offset, Math.Max(0, content.Length - 1));
        }

        var lines = new List<string>();
        var end = bounded ? Math.Min(content.Length, _offset + viewportHeight) : content.Length;
        for (var i = _offset; i < end; i++)
            lines.Add(content[i]);

        // pad only for a real bounded viewport; int.MaxValue means "unbounded" → no padding
        if (bounded)
        {
            while (lines.Count < viewportHeight)
                lines.Add("");
        }
        return [.. lines];
    }

    // Unbounded content height: expose the content from the scroll offset down.
    // 05-H2: this used to return the child verbatim, so _offset (ScrollBy /
    // ScrollToEnd) never influenced any output.
    public string[] Render(int width) => RenderViewport(width, int.MaxValue);

    /// <summary>Bounded render (05-C2): the allocated height is the viewport.</summary>
    public string[] Render(int width, int height) =>
        height == int.MaxValue ? Render(width) : RenderViewport(width, height);

    public void HandleInput(string seq) { }

    public void Invalidate() => _child.Invalidate();
}
