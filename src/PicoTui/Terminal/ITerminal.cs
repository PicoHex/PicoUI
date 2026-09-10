namespace PicoTui.Terminal;

public interface ITerminal
{
    void Start(Action<string> onInput, Action onResize);
    void Stop();
    void Write(string data);
    int Columns { get; }
    int Rows { get; }
    void MoveBy(int lines);
    void HideCursor();
    void ShowCursor();
    void ClearLine();
    void ClearFromCursor();
    void ClearScreen();
    void SetTitle(string title);
    Task DrainInputAsync(int maxMs, int idleMs);
}
