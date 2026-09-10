namespace PicoTui.Layout;

public sealed class ToolCallWidget : IComponent
{
    private string _tool = "";
    private string _args = "";
    private bool _executing;
    private string _result = "";

    public void Start(string toolName)
    {
        _tool = toolName;
        Invalidate();
    }

    public void AppendArgs(string delta)
    {
        _args += delta;
        Invalidate();
    }

    public void SetExecuting(bool executing)
    {
        _executing = executing;
        Invalidate();
    }

    public void SetResult(string? result)
    {
        _result = result ?? "";
        _executing = false;
        Invalidate();
    }

    public string[] Render(int width)
    {
        var result = new List<string> { $"⚙ {_tool}({_args})" };
        if (_executing)
            result.Add("   running…");
        else if (_result.Length > 0)
            result.Add($"   {_result}");
        return [.. result.Select(l => l.Length > width ? l[..width] : l)];
    }

    public void HandleInput(string seq) { }

    public void Invalidate() { }
}
