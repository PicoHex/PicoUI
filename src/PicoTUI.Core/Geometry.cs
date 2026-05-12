namespace PicoTUI;

/// <summary>A column/row position in the terminal.</summary>
public readonly record struct Point(int X, int Y)
{
    public static readonly Point Zero = new(0, 0);

    public Point Offset(int dx, int dy) => new(X + dx, Y + dy);
    public Point Offset(Size s) => new(X + s.Width, Y + s.Height);

    public override string ToString() => $"({X}, {Y})";
}

/// <summary>A width/height measurement in terminal columns/rows.</summary>
public readonly record struct Size(int Width, int Height)
{
    public static readonly Size Zero = new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
    public int Area => Width * Height;

    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>A rectangular region described by its top-left corner, width, and height.</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public static readonly Rect Empty = new(0, 0, 0, 0);

    public int Right => X + Width;
    public int Bottom => Y + Height;

    public Point TopLeft => new(X, Y);
    public Point TopRight => new(Right, Y);
    public Point BottomLeft => new(X, Bottom);
    public Point BottomRight => new(Right, Bottom);

    public Size Size => new(Width, Height);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
    public bool Contains(Point p) => Contains(p.X, p.Y);

    /// <summary>Returns an inner rect with the given margin removed from each side.</summary>
    public Rect Shrink(int all) => Shrink(all, all, all, all);

    /// <summary>Returns an inner rect with the given margins removed (top, right, bottom, left).</summary>
    public Rect Shrink(int top, int right, int bottom, int left)
    {
        int x = X + left;
        int y = Y + top;
        int w = Width - left - right;
        int h = Height - top - bottom;
        return w <= 0 || h <= 0 ? Empty : new Rect(x, y, w, h);
    }

    public Rect Shrink(Thickness t) => Shrink(t.Top, t.Right, t.Bottom, t.Left);

    /// <summary>Clips this rect to the given bounds.</summary>
    public Rect Intersect(Rect other)
    {
        int x = Math.Max(X, other.X);
        int y = Math.Max(Y, other.Y);
        int r = Math.Min(Right, other.Right);
        int b = Math.Min(Bottom, other.Bottom);
        return r > x && b > y ? new Rect(x, y, r - x, b - y) : Empty;
    }

    public static Rect FromLTRB(int left, int top, int right, int bottom) =>
        new(left, top, right - left, bottom - top);

    public override string ToString() => $"({X},{Y}) {Width}x{Height}";
}

/// <summary>Padding or margin values for all four sides.</summary>
public readonly record struct Thickness(int Left, int Top, int Right, int Bottom)
{
    public static readonly Thickness Zero = new(0, 0, 0, 0);

    public Thickness(int all) : this(all, all, all, all) { }

    public Thickness(int horizontal, int vertical) : this(horizontal, vertical, horizontal, vertical) { }
}
