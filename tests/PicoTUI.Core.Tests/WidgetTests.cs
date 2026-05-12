using PicoTUI.Widgets;

namespace PicoTUI.Core.Tests;

public class WidgetTests
{
    // ── Label ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Label_Render_DrawsText()
    {
        var canvas = new Canvas(20, 5);
        var label = new Label
        {
            Bounds = new Rect(0, 0, 20, 1),
            Text = "Hello",
            Style = Style.Default
        };

        label.Render(canvas);

        Assert.Equal('H', canvas.GetCell(0, 0).Character);
        Assert.Equal('e', canvas.GetCell(1, 0).Character);
    }

    [Fact]
    public void Label_Render_TruncatesLongText()
    {
        var canvas = new Canvas(5, 1);
        var label = new Label
        {
            Bounds = new Rect(0, 0, 5, 1),
            Text = "Hello World!"
        };

        label.Render(canvas);

        Assert.Equal('H', canvas.GetCell(0, 0).Character);
        Assert.Equal('o', canvas.GetCell(4, 0).Character);
        // " World!" is clipped
    }

    [Fact]
    public void Label_Render_NotVisible_DoesNotDraw()
    {
        var canvas = new Canvas(20, 5);
        var label = new Label
        {
            Bounds = new Rect(0, 0, 20, 1),
            Text = "Hello",
            IsVisible = false
        };

        label.Render(canvas);

        Assert.Equal(' ', canvas.GetCell(0, 0).Character);
    }

    [Fact]
    public void Label_Render_EmptyBounds_DoesNotThrow()
    {
        var canvas = new Canvas(20, 5);
        var label = new Label { Bounds = Rect.Empty, Text = "Hi" };
        label.Render(canvas); // should not throw
    }

    [Fact]
    public void Label_CenterAlignment_PadsText()
    {
        var canvas = new Canvas(20, 1);
        var label = new Label
        {
            Bounds = new Rect(0, 0, 20, 1),
            Text = "Hi",
            Alignment = Alignment.Center
        };
        label.Render(canvas);

        // "Hi" centered in 20 chars → padded with spaces
        // The text characters should not be at the very start
        bool foundH = false;
        for (int x = 1; x < 19; x++)
        {
            if (canvas.GetCell(x, 0).Character == 'H') { foundH = true; break; }
        }
        Assert.True(foundH, "Centered 'H' should not be at x=0");
    }

    // ── Block ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Block_Render_DrawsBorder()
    {
        var canvas = new Canvas(20, 10);
        var block = new Block
        {
            Bounds = new Rect(0, 0, 20, 10),
            BorderSet = BorderSet.Single
        };

        block.Render(canvas);

        Assert.Equal('┌', canvas.GetCell(0, 0).Character);
        Assert.Equal('┐', canvas.GetCell(19, 0).Character);
        Assert.Equal('└', canvas.GetCell(0, 9).Character);
        Assert.Equal('┘', canvas.GetCell(19, 9).Character);
    }

    [Fact]
    public void Block_Render_DrawsTitle()
    {
        var canvas = new Canvas(20, 5);
        var block = new Block
        {
            Bounds = new Rect(0, 0, 20, 5),
            BorderSet = BorderSet.Single,
            Title = "Test"
        };

        block.Render(canvas);

        // Title starts at x=1 (after the corner char) by default (left aligned)
        Assert.Equal('T', canvas.GetCell(1, 0).Character);
        Assert.Equal('e', canvas.GetCell(2, 0).Character);
        Assert.Equal('s', canvas.GetCell(3, 0).Character);
        Assert.Equal('t', canvas.GetCell(4, 0).Character);
    }

    [Fact]
    public void Block_InnerBounds_IsShrunkByOne()
    {
        var block = new Block { Bounds = new Rect(5, 5, 20, 10) };
        var inner = block.InnerBounds;
        Assert.Equal(new Rect(6, 6, 18, 8), inner);
    }

    // ── Paragraph ─────────────────────────────────────────────────────────────

    [Fact]
    public void Paragraph_Render_DrawsLines()
    {
        var canvas = new Canvas(20, 5);
        var para = new Paragraph { Bounds = new Rect(0, 0, 20, 5) };
        para.WithLines("Line 1", "Line 2");

        para.Render(canvas);

        Assert.Equal('L', canvas.GetCell(0, 0).Character);
        Assert.Equal('L', canvas.GetCell(0, 1).Character);
        Assert.Equal('i', canvas.GetCell(1, 0).Character);
    }

    [Fact]
    public void Paragraph_Render_WrapsLongLines()
    {
        var canvas = new Canvas(5, 5);
        var para = new Paragraph { Bounds = new Rect(0, 0, 5, 5), Wrap = true };
        para.WithLines("ABCDEFGHIJ"); // 10 chars → 2 rows of 5

        para.Render(canvas);

        Assert.Equal('A', canvas.GetCell(0, 0).Character);
        Assert.Equal('E', canvas.GetCell(4, 0).Character);
        Assert.Equal('F', canvas.GetCell(0, 1).Character);
        Assert.Equal('J', canvas.GetCell(4, 1).Character);
    }

    // ── Gauge ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Gauge_Render_FilledAtHalf()
    {
        var canvas = new Canvas(10, 1);
        var gauge = new Gauge
        {
            Bounds = new Rect(0, 0, 10, 1),
            Ratio = 0.5,
            FilledStyle = new Style(Color.Default, Color.Green, TextAttributes.None),
            EmptyStyle = new Style(Color.Default, Color.Red, TextAttributes.None)
        };

        gauge.Render(canvas);

        // First 5 cells should be green-background (filled)
        for (int x = 0; x < 5; x++)
            Assert.Equal(Color.Green, canvas.GetCell(x, 0).Style.Background);

        // Last 5 cells should be red-background (empty)
        for (int x = 5; x < 10; x++)
            Assert.Equal(Color.Red, canvas.GetCell(x, 0).Style.Background);
    }

    [Fact]
    public void Gauge_Render_ZeroRatio_AllEmpty()
    {
        var canvas = new Canvas(10, 1);
        var gauge = new Gauge
        {
            Bounds = new Rect(0, 0, 10, 1),
            Ratio = 0.0,
            FilledStyle = new Style(Color.Default, Color.Green, TextAttributes.None),
            EmptyStyle = new Style(Color.Default, Color.Red, TextAttributes.None)
        };

        gauge.Render(canvas);

        for (int x = 0; x < 10; x++)
            Assert.Equal(Color.Red, canvas.GetCell(x, 0).Style.Background);
    }

    [Fact]
    public void Gauge_Render_FullRatio_AllFilled()
    {
        var canvas = new Canvas(10, 1);
        var gauge = new Gauge
        {
            Bounds = new Rect(0, 0, 10, 1),
            Ratio = 1.0,
            FilledStyle = new Style(Color.Default, Color.Green, TextAttributes.None),
            EmptyStyle = new Style(Color.Default, Color.Red, TextAttributes.None)
        };

        gauge.Render(canvas);

        for (int x = 0; x < 10; x++)
            Assert.Equal(Color.Green, canvas.GetCell(x, 0).Style.Background);
    }

    // ── List ─────────────────────────────────────────────────────────────────

    [Fact]
    public void List_AddItem_SelectsFirst()
    {
        var list = new List();
        list.AddItem("Item 1").AddItem("Item 2");
        Assert.Equal(0, list.SelectedIndex);
        Assert.Equal("Item 1", list.SelectedItem?.Text);
    }

    [Fact]
    public void List_MoveDown_IncrementsSelection()
    {
        var list = new List();
        list.AddItem("A").AddItem("B").AddItem("C");
        list.Bounds = new Rect(0, 0, 20, 5);
        list.MoveDown();
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void List_MoveUp_DecrementsSelection()
    {
        var list = new List();
        list.AddItem("A").AddItem("B").AddItem("C");
        list.Bounds = new Rect(0, 0, 20, 5);
        list.SelectedIndex = 2;
        list.MoveUp();
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void List_MoveUp_DoesNotGoNegative()
    {
        var list = new List();
        list.AddItem("A");
        list.Bounds = new Rect(0, 0, 20, 5);
        list.MoveUp();
        Assert.Equal(0, list.SelectedIndex);
    }

    [Fact]
    public void List_MoveDown_DoesNotExceedCount()
    {
        var list = new List();
        list.AddItem("A");
        list.Bounds = new Rect(0, 0, 20, 5);
        list.MoveDown();
        Assert.Equal(0, list.SelectedIndex);
    }

    [Fact]
    public void List_Render_HighlightsSelectedItem()
    {
        var canvas = new Canvas(20, 3);
        var list = new List
        {
            Bounds = new Rect(0, 0, 20, 3),
            SelectedStyle = new Style(Color.Black, Color.Cyan, TextAttributes.None),
            ItemStyle = Style.Default
        };
        list.AddItem("Alpha").AddItem("Beta").AddItem("Gamma");

        list.Render(canvas);

        // Row 0 (selected) should have cyan background
        Assert.Equal(Color.Cyan, canvas.GetCell(0, 0).Style.Background);
        // Row 1 (unselected) should have default background
        Assert.Equal(Color.Default, canvas.GetCell(0, 1).Style.Background);
    }
}
