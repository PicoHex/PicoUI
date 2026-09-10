namespace PicoTui.Tests.Layout;

public sealed class SelectListTests
{
    [Test]
    public async Task MoveSelection_ChangesSelected()
    {
        var list = new SelectList();
        list.AddItem("a");
        list.AddItem("b");
        list.MoveSelection(1);
        await Assert.That(list.Selected).IsEqualTo(1);
        var lines = list.Render(20);
        await Assert.That(lines[1].Contains("b")).IsTrue();
    }
}
