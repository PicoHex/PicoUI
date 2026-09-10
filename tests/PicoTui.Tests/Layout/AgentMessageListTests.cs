namespace PicoTui.Tests.Layout;

public sealed class AgentMessageListTests
{
    [Test]
    public async Task AddUser_RendersUserLine()
    {
        var m = new AgentMessageList();
        m.AddUser("hello");
        var lines = m.Render(40);
        await Assert.That(lines[0].Contains("hello")).IsTrue();
    }

    [Test]
    public async Task AppendAssistantText_StreamsToLast()
    {
        var m = new AgentMessageList();
        m.AppendAssistantText("ab");
        m.AppendAssistantText("cd");
        var lines = m.Render(40);
        await Assert.That(lines[0]).IsEqualTo("abcd");
    }

    [Test]
    public async Task BeginThinking_AddsCollapsibleBlock()
    {
        var m = new AgentMessageList();
        var tb = m.BeginThinking();
        tb.Append("deep thought");
        var lines = m.Render(40);
        await Assert.That(lines[0].Contains("thinking")).IsTrue();
    }
}
