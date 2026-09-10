using InputLine = PicoTui.Layout.Input;

namespace PicoTui.Tests.Layout;

public sealed class InputTests
{
    [Test]
    public async Task Typing_Appends_And_HorizontallyScrolls()
    {
        var input = new InputLine();
        input.HandleInput("a");
        input.HandleInput("b");
        await Assert.That(input.Text).IsEqualTo("ab");
        var line = input.Render(2)[0];
        await Assert.That(line.Length).IsLessThanOrEqualTo(2);
    }
}
