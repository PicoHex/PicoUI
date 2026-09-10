namespace PicoTui.Screen;

public readonly record struct DiffResult(
    int FirstDiffLine,
    int LastDiffLine,
    bool NeedsFullRedraw,
    bool WidthChanged
);

public sealed class DiffRenderer
{
    public static DiffResult Compute(ScreenBuffer prev, ScreenBuffer next)
    {
        if (prev.Cols != next.Cols)
            return new DiffResult(0, next.Rows - 1, true, true);
        if (prev.Rows != next.Rows)
            return new DiffResult(0, Math.Max(prev.Rows, next.Rows) - 1, true, false);

        var first = -1;
        var last = -1;
        for (var r = 0; r < next.Rows; r++)
        {
            var changed = !SameLine(prev, next, r);
            if (changed)
            {
                if (first < 0)
                    first = r;
                last = r;
            }
        }

        if (first < 0)
            return new DiffResult(-1, -1, false, false);

        var changedCount = last - first + 1;
        var needsFull = changedCount > next.Rows / 2;
        return new DiffResult(first, last, needsFull, false);
    }

    private static bool SameLine(ScreenBuffer a, ScreenBuffer b, int r)
    {
        for (var c = 0; c < a.Cols; c++)
            if (a.Get(r, c) != b.Get(r, c))
                return false;
        return true;
    }
}
