using InputComponent = PicoTui.Layout.Input;

namespace PicoTui.Tests.Layout;

/// <summary>
/// The cursor properties are part of the public surface: they must reflect the
/// real insertion point (previously both were never assigned and stayed 0).
/// </summary>
public sealed class CursorStateTests
{
    [Test]
    public async Task Input_Cursor_TracksEndOfText()
    {
        var input = new InputComponent();

        input.HandleInput("a");
        input.HandleInput("b");
        await Assert.That(input.Cursor).IsEqualTo(2);

        input.HandleInput("\b");
        await Assert.That(input.Cursor).IsEqualTo(1);
    }

    [Test]
    public async Task Editor_CursorCol_TracksLastLine()
    {
        var editor = new Editor();
        editor.SetText("hello");
        await Assert.That(editor.CursorCol).IsEqualTo(5);

        editor.HandleInput("x");
        await Assert.That(editor.CursorCol).IsEqualTo(6);

        editor.HandleInput("\r");
        await Assert.That(editor.CursorCol).IsEqualTo(0);

        editor.HandleInput("y");
        await Assert.That(editor.CursorCol).IsEqualTo(1);

        editor.HandleInput("\b");
        await Assert.That(editor.CursorCol).IsEqualTo(0);
    }

    [Test]
    public async Task Editor_CursorCol_CountsDisplayWidth()
    {
        var editor = new Editor();
        editor.SetText("中文");

        await Assert.That(editor.CursorCol).IsEqualTo(4);
    }
}
