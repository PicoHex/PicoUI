namespace PicoTui.Tests.Layout;

public sealed class ToolCallWidgetTests
{
    [Test]
    public async Task Start_RendersToolName()
    {
        var w = new ToolCallWidget();
        w.Start("read");
        var lines = w.Render(40);
        await Assert.That(lines[0].Contains("read")).IsTrue();
    }

    [Test]
    public async Task Executing_RendersStatus()
    {
        var w = new ToolCallWidget();
        w.Start("read");
        w.SetExecuting(true);
        var lines = w.Render(40);
        await Assert.That(lines.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(lines[1].Contains("running")).IsTrue();
    }

    [Test]
    public async Task Result_RendersSummary()
    {
        var w = new ToolCallWidget();
        w.Start("read");
        w.SetResult("42 chars");
        var lines = w.Render(40);
        await Assert.That(lines[^1].Contains("42 chars")).IsTrue();
    }
}
