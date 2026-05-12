namespace PicoTUI.Core.Tests;

public class GeometryTests
{
    // ── Point ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Point_Zero_HasZeroCoordinates()
    {
        Assert.Equal(0, Point.Zero.X);
        Assert.Equal(0, Point.Zero.Y);
    }

    [Fact]
    public void Point_Offset_AddsDeltas()
    {
        var p = new Point(3, 4);
        var q = p.Offset(1, -2);
        Assert.Equal(4, q.X);
        Assert.Equal(2, q.Y);
    }

    [Fact]
    public void Point_Offset_BySize()
    {
        var p = new Point(1, 2);
        var s = new Size(10, 20);
        var q = p.Offset(s);
        Assert.Equal(11, q.X);
        Assert.Equal(22, q.Y);
    }

    [Fact]
    public void Point_Equality()
    {
        Assert.Equal(new Point(5, 6), new Point(5, 6));
        Assert.NotEqual(new Point(5, 6), new Point(6, 5));
    }

    // ── Size ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Size_IsEmpty_WhenWidthIsZero()
    {
        Assert.True(new Size(0, 10).IsEmpty);
    }

    [Fact]
    public void Size_IsEmpty_WhenHeightIsZero()
    {
        Assert.True(new Size(10, 0).IsEmpty);
    }

    [Fact]
    public void Size_IsEmpty_WhenNegative()
    {
        Assert.True(new Size(-1, -1).IsEmpty);
    }

    [Fact]
    public void Size_NotEmpty_WhenPositive()
    {
        Assert.False(new Size(1, 1).IsEmpty);
    }

    [Fact]
    public void Size_Area()
    {
        Assert.Equal(12, new Size(3, 4).Area);
    }

    // ── Rect ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Rect_Edges()
    {
        var r = new Rect(2, 3, 10, 5);
        Assert.Equal(12, r.Right);
        Assert.Equal(8, r.Bottom);
    }

    [Fact]
    public void Rect_Contains_IncludesTopLeft()
    {
        var r = new Rect(2, 3, 10, 5);
        Assert.True(r.Contains(2, 3));
    }

    [Fact]
    public void Rect_Contains_ExcludesRight()
    {
        var r = new Rect(2, 3, 10, 5);
        Assert.False(r.Contains(12, 3));
    }

    [Fact]
    public void Rect_Contains_ExcludesBottom()
    {
        var r = new Rect(2, 3, 10, 5);
        Assert.False(r.Contains(2, 8));
    }

    [Fact]
    public void Rect_Shrink_All()
    {
        var r = new Rect(0, 0, 10, 8);
        var s = r.Shrink(1);
        Assert.Equal(new Rect(1, 1, 8, 6), s);
    }

    [Fact]
    public void Rect_Shrink_ToEmpty_WhenTooSmall()
    {
        var r = new Rect(0, 0, 2, 2);
        var s = r.Shrink(2);
        Assert.True(s.IsEmpty);
    }

    [Fact]
    public void Rect_Intersect_OverlappingRects()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);
        var i = a.Intersect(b);
        Assert.Equal(new Rect(5, 5, 5, 5), i);
    }

    [Fact]
    public void Rect_Intersect_NonOverlappingRects_ReturnsEmpty()
    {
        var a = new Rect(0, 0, 5, 5);
        var b = new Rect(10, 10, 5, 5);
        var i = a.Intersect(b);
        Assert.True(i.IsEmpty);
    }

    [Fact]
    public void Rect_FromLTRB()
    {
        var r = Rect.FromLTRB(1, 2, 11, 12);
        Assert.Equal(new Rect(1, 2, 10, 10), r);
    }

    // ── Thickness ─────────────────────────────────────────────────────────────

    [Fact]
    public void Thickness_SingleValue()
    {
        var t = new Thickness(3);
        Assert.Equal(3, t.Left);
        Assert.Equal(3, t.Top);
        Assert.Equal(3, t.Right);
        Assert.Equal(3, t.Bottom);
    }

    [Fact]
    public void Thickness_HorizontalVertical()
    {
        var t = new Thickness(2, 4);
        Assert.Equal(2, t.Left);
        Assert.Equal(2, t.Right);
        Assert.Equal(4, t.Top);
        Assert.Equal(4, t.Bottom);
    }

    [Fact]
    public void Rect_Shrink_WithThickness()
    {
        var r = new Rect(0, 0, 20, 10);
        var t = new Thickness(2, 1, 3, 1);  // left, top, right, bottom
        var s = r.Shrink(t);
        Assert.Equal(new Rect(2, 1, 15, 8), s);
    }
}
