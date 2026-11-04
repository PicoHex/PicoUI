namespace PicoTui.Layout;

public sealed class VStack : IComponent
{
    private readonly List<LayoutItem> _items = [];

    public void Add(LayoutItem item) => _items.Add(item);

    public string[] Render(int width)
    {
        // Unbounded content height: render children at natural height (no allocation).
        var visible = _items.Where(i => i.Visible is null || i.Visible(width, 0)).ToList();
        var result = new List<string>();
        foreach (var item in visible)
            result.AddRange(item.Component.Render(width));
        return [.. result];
    }

    /// <summary>
    /// Layout pass over <c>(width, availableHeight)</c> per spec §6.2:
    /// natural/basis allocation, grow distribution, shrink on overflow.
    /// </summary>
    /// <remarks>Grow shares are computed from the frozen remaining total and the
    /// rounding remainder is handed out one row at a time in (Grow descending,
    /// declaration order) — see the pass below.</remarks>
    public string[] Render(int width, int availableHeight)
    {
        var visible = _items
            .Where(i => i.Visible is null || i.Visible(width, availableHeight))
            .ToList();
        if (visible.Count == 0)
            return [];

        // 1-4: natural heights (Basis>0 → fixed, clamped by min/max)
        var heights = new int[visible.Count];
        for (var i = 0; i < visible.Count; i++)
        {
            var item = visible[i];
            var natural = item.Basis > 0 ? item.Basis : item.Component.Render(width).Length;
            heights[i] = Math.Clamp(natural, item.MinSize, item.MaxSize);
        }

        var allocated = heights.Sum();

        // 5-6: remaining > 0 → distribute by Grow weight among Grow>0 items.
        if (allocated < availableHeight)
        {
            var pool = availableHeight - allocated;
            var growTotal = visible.Sum(i => Math.Max(0, i.Grow));
            if (growTotal > 0)
            {
                // 05-H1: the numerator is the FROZEN remaining total, not a
                // running remainder that has already been decremented — otherwise
                // equal weights split 7:3 instead of 5:5 / 20:20.
                var total = pool;
                for (var i = 0; i < visible.Count; i++)
                {
                    if (visible[i].Grow <= 0)
                        continue;
                    var share = total * Math.Max(0, visible[i].Grow) / growTotal;
                    var headroom = Math.Max(0, visible[i].MaxSize - heights[i]);
                    var add = Math.Min(share, headroom);
                    heights[i] += add;
                    pool -= add;
                }

                // Deterministic rule for the remainder (integer rounding plus
                // shares dropped by MaxSize): hand out one row at a time in
                // (Grow descending, declaration order) while any headroom is left,
                // instead of dropping the whole remainder on the first item.
                var order = Enumerable
                    .Range(0, visible.Count)
                    .Where(i => visible[i].Grow > 0)
                    .OrderByDescending(i => visible[i].Grow)
                    .ThenBy(i => i)
                    .ToList();
                while (pool > 0)
                {
                    var progressed = false;
                    foreach (var i in order)
                    {
                        if (pool <= 0)
                            break;
                        if (heights[i] >= visible[i].MaxSize)
                            continue;
                        heights[i]++;
                        pool--;
                        progressed = true;
                    }
                    if (!progressed)
                        break; // every grow item is at MaxSize
                }
            }
        }
        // 7: remaining < 0 (overflow) → shrink by Shrink weight, down to MinSize
        else if (allocated > availableHeight)
        {
            var overflow = allocated - availableHeight;
            while (overflow > 0)
            {
                var shrinkTotal = 0;
                var anyShrinkable = false;
                for (var i = 0; i < visible.Count; i++)
                {
                    if (visible[i].Shrink <= 0 || heights[i] <= visible[i].MinSize)
                        continue;
                    shrinkTotal += visible[i].Shrink;
                    anyShrinkable = true;
                }
                if (!anyShrinkable)
                    break;
                var shrank = false;
                for (var i = 0; i < visible.Count && overflow > 0; i++)
                {
                    if (visible[i].Shrink <= 0)
                        continue;
                    var room = heights[i] - visible[i].MinSize;
                    if (room <= 0)
                        continue;
                    var share = overflow * visible[i].Shrink / shrinkTotal;
                    share = Math.Min(share, room);
                    heights[i] -= share;
                    overflow -= share;
                    shrank |= share > 0;
                }
                if (!shrank)
                    break;
            }
        }

        // 8: render each item, take the allocated slice
        var result = new List<string>();
        for (var i = 0; i < visible.Count; i++)
        {
            // 05-C2: the allocated height goes down to the child, so a scroll view
            // clips to its viewport instead of being sliced after the fact.
            var lines = visible[i].Component.Render(width, heights[i]);
            var h = heights[i];
            for (var l = 0; l < h; l++)
                result.Add(l < lines.Length ? lines[l] : "");
        }
        return [.. result];
    }

    public void HandleInput(string seq) { }

    public void Invalidate()
    {
        foreach (var item in _items)
            item.Component.Invalidate();
    }
}
