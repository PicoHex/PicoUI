namespace PicoTui.Terminal;

public sealed class VirtualTerminal : ITerminal
{
    public VirtualTerminal(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public int Columns { get; private set; }
    public int Rows { get; private set; }
    public string Output { get; set; } = "";
    private Action<string>? _onInput;
    private Action? _onResize;

    public void Start(Action<string> onInput, Action onResize)
    {
        _onInput = onInput;
        _onResize = onResize;
    }

    public void Stop() { }

    public void Write(string data) => Output += data;

    public void InjectInput(string data) => _onInput?.Invoke(data);

    public void Resize(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        _onResize?.Invoke();
    }

    public void MoveBy(int lines) => Write($"\x1b[{lines}A");

    public void HideCursor() => Write("\x1b[?25l");

    public void ShowCursor() => Write("\x1b[?25h");

    public void ClearLine() => Write("\x1b[2K");

    public void ClearFromCursor() => Write("\x1b[0K");

    public void ClearScreen() => Write("\x1b[2J");

    public void SetTitle(string title) => Write($"\x1b]0;{title}\x07");

    public Task DrainInputAsync(int maxMs, int idleMs) => Task.CompletedTask;
}
