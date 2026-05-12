using PicoTUI.Events;

namespace PicoTUI.Core.Tests;

public class CellTests
{
    [Fact]
    public void Cell_Empty_IsSpace()
    {
        Assert.Equal(' ', Cell.Empty.Character);
        Assert.Equal(Style.Default, Cell.Empty.Style);
    }

    [Fact]
    public void Cell_IsBlank_WhenSpace()
    {
        var cell = new Cell(' ', Style.Default);
        Assert.True(cell.IsBlank);
    }

    [Fact]
    public void Cell_IsNotBlank_WhenNotSpace()
    {
        var cell = new Cell('X', Style.Default);
        Assert.False(cell.IsBlank);
    }

    [Fact]
    public void Cell_Equality()
    {
        var a = new Cell('A', Style.Default);
        var b = new Cell('A', Style.Default);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Cell_Inequality_DifferentChar()
    {
        var a = new Cell('A', Style.Default);
        var b = new Cell('B', Style.Default);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Cell_Inequality_DifferentStyle()
    {
        var a = new Cell('A', Style.Default);
        var b = new Cell('A', Style.Default.WithForeground(Color.Red));
        Assert.NotEqual(a, b);
    }
}

public class BorderSetTests
{
    [Fact]
    public void BorderSet_Single_HasExpectedChars()
    {
        Assert.Equal('┌', BorderSet.Single.TopLeft);
        Assert.Equal('─', BorderSet.Single.Top);
        Assert.Equal('┐', BorderSet.Single.TopRight);
        Assert.Equal('│', BorderSet.Single.Left);
        Assert.Equal('┘', BorderSet.Single.BottomRight);
    }

    [Fact]
    public void BorderSet_Double_HasExpectedChars()
    {
        Assert.Equal('╔', BorderSet.Double.TopLeft);
        Assert.Equal('═', BorderSet.Double.Top);
        Assert.Equal('╗', BorderSet.Double.TopRight);
        Assert.Equal('║', BorderSet.Double.Left);
    }

    [Fact]
    public void BorderSet_Rounded_HasRoundedCorners()
    {
        Assert.Equal('╭', BorderSet.Rounded.TopLeft);
        Assert.Equal('╮', BorderSet.Rounded.TopRight);
        Assert.Equal('╯', BorderSet.Rounded.BottomRight);
        Assert.Equal('╰', BorderSet.Rounded.BottomLeft);
    }

    [Fact]
    public void BorderSet_Ascii_HasAsciiChars()
    {
        Assert.Equal('+', BorderSet.Ascii.TopLeft);
        Assert.Equal('-', BorderSet.Ascii.Top);
        Assert.Equal('|', BorderSet.Ascii.Left);
    }
}

public class EventTests
{
    [Fact]
    public void KeyEvent_IsCtrl_ReturnsTrueForMatchingCtrlChar()
    {
        var ev = new KeyEvent(Key.Char, 'c', KeyModifiers.Control);
        Assert.True(ev.IsCtrl('c'));
        Assert.False(ev.IsCtrl('d'));
    }

    [Fact]
    public void KeyEvent_IsCtrl_ReturnsFalseWithoutCtrl()
    {
        var ev = new KeyEvent(Key.Char, 'c', KeyModifiers.None);
        Assert.False(ev.IsCtrl('c'));
    }

    [Fact]
    public void ResizeEvent_StoresSize()
    {
        var ev = new ResizeEvent(new Size(80, 24));
        Assert.Equal(80, ev.Size.Width);
        Assert.Equal(24, ev.Size.Height);
    }

    [Fact]
    public void MouseEvent_StoresProperties()
    {
        var ev = new MouseEvent(new Point(10, 5), MouseButton.Left, MouseAction.Press, KeyModifiers.Shift);
        Assert.Equal(10, ev.Position.X);
        Assert.Equal(5, ev.Position.Y);
        Assert.Equal(MouseButton.Left, ev.Button);
        Assert.Equal(MouseAction.Press, ev.Action);
        Assert.Equal(KeyModifiers.Shift, ev.Modifiers);
    }

    [Fact]
    public void KeyModifiers_FlagsWork()
    {
        var mods = KeyModifiers.Control | KeyModifiers.Shift;
        Assert.True((mods & KeyModifiers.Control) != 0);
        Assert.True((mods & KeyModifiers.Shift) != 0);
        Assert.False((mods & KeyModifiers.Alt) != 0);
    }
}
