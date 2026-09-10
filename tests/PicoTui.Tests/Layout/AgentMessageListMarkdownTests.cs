namespace PicoTui.Tests.Layout;

public sealed class AgentMessageListMarkdownTests
{
    [Test]
    public async Task AssistantText_RendersAsMarkdown()
    {
        var m = new AgentMessageList();
        m.AppendAssistantText("hello **world**");
        var lines = m.Render(40);
        await Assert.That(lines[0]).IsEqualTo("hello world");
    }

    [Test]
    public async Task AssistantText_CodeBlock_Preserved()
    {
        var m = new AgentMessageList();
        m.AppendAssistantText("```cs\nint x;\n```");
        var lines = m.Render(40);
        await Assert.That(lines[0].Contains("int x;")).IsTrue();
    }
}
