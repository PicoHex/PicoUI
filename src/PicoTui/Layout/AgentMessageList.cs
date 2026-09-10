namespace PicoTui.Layout;

public sealed class AgentMessageList : IComponent
{
    private readonly List<IComponent> _rows = [];
    private string _tailText = "";
    private IComponent? _tail;

    public void AddUser(string text)
    {
        _rows.Add(new Text(text));
        _tail = null;
    }

    public void AppendAssistantText(string delta)
    {
        if (_tail is not MarkdownComponent)
        {
            _tail = new MarkdownComponent("");
            _rows.Add(_tail);
        }
        _tailText += delta;
        ((MarkdownComponent)_tail).SetText(_tailText);
    }

    public ThinkingBlock BeginThinking()
    {
        var tb = new ThinkingBlock();
        _rows.Add(tb);
        _tail = tb;
        return tb;
    }

    public ToolCallWidget BeginToolCall()
    {
        var w = new ToolCallWidget();
        _rows.Add(w);
        _tail = w;
        return w;
    }

    public void FinishMessage()
    {
        _tail = null;
        _tailText = "";
    }

    public string[] Render(int width)
    {
        var result = new List<string>();
        foreach (var row in _rows)
            result.AddRange(row.Render(width));
        return [.. result];
    }

    public void HandleInput(string seq) { }

    public void Invalidate()
    {
        foreach (var row in _rows)
            row.Invalidate();
    }
}
