namespace PicoTui.Tests.Layout;

/// <summary>Switching between message segments must not carry the previous
/// segment's accumulated text into the next one.</summary>
public sealed class AgentMessageListStateTests
{
    [Test]
    public async Task AppendAfterThinking_DoesNotBleedPreviousText()
    {
        var list = new AgentMessageList();
        list.AppendAssistantText("alpha");
        list.BeginThinking();
        list.AppendAssistantText("beta");

        var lines = list.Render(40);

        await Assert.That(lines.Any(l => l == "beta")).IsTrue();
        await Assert.That(lines.Any(l => l.Contains("alphabeta"))).IsFalse();
    }

    [Test]
    public async Task AppendAfterUserMessage_DoesNotBleedPreviousText()
    {
        var list = new AgentMessageList();
        list.AppendAssistantText("alpha");
        list.AddUser("question");
        list.AppendAssistantText("beta");

        var lines = list.Render(40);

        await Assert.That(lines.Any(l => l == "beta")).IsTrue();
        await Assert.That(lines.Any(l => l.Contains("alphabeta"))).IsFalse();
    }
}
