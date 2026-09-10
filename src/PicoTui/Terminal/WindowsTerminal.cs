namespace PicoTui.Terminal;

public sealed class WindowsTerminal : ITerminal
{
    private int _cols = 80;
    private int _rows = 24;
    private readonly CancellationTokenSource _cts = new();

    // audit L9: incremental UTF-8 decoding — a 4096-byte stdin block can split
    // a multi-byte character; per-chunk decoding corrupted CJK input.
    private readonly PicoTui.Input.Utf8ChunkDecoder _decoder = new();

    public WindowsTerminal()
    {
        QuerySize();
    }

    public int Columns => _cols;
    public int Rows => _rows;

    public void Start(Action<string> onInput, Action onResize)
    {
        EnableVtMode();
        _ = Task.Run(
            async () =>
            {
                var buf = new byte[4096];
                while (!_cts.IsCancellationRequested)
                {
                    var n = Console.OpenStandardInput().Read(buf, 0, buf.Length);
                    if (n > 0)
                        onInput(_decoder.Decode(buf, 0, n));
                }
            },
            _cts.Token
        );
        // Resize polling (v1): GetConsoleScreenBufferInfo is cheap; SIGWINCH has
        // no Windows equivalent. Hook retained for a future event-driven path.
    }

    public void Stop() => _cts.Cancel();

    public void Write(string data) => Console.Out.Write(data);

    public void MoveBy(int lines) => Write($"\x1b[{lines}A");

    public void HideCursor() => Write("\x1b[?25l");

    public void ShowCursor() => Write("\x1b[?25h");

    public void ClearLine() => Write("\x1b[2K");

    public void ClearFromCursor() => Write("\x1b[0K");

    public void ClearScreen() => Write("\x1b[2J");

    public void SetTitle(string title) => Write($"\x1b]0;{TitleSanitizer.Sanitize(title)}\x07");

    public Task DrainInputAsync(int maxMs, int idleMs) => Task.Delay(Math.Min(maxMs, 50));

    private static void EnableVtMode()
    {
        try
        {
            var output = ConsoleApiNative.GetStdHandle(ConsoleApiNative.StdOutputHandle);
            if (output != -1 && ConsoleApiNative.GetConsoleMode(output, out var mode) != 0)
                ConsoleApiNative.SetConsoleMode(output, ConsoleApiNative.WithVtMode(mode));
            var input = ConsoleApiNative.GetStdHandle(ConsoleApiNative.StdInputHandle);
            if (input != -1 && ConsoleApiNative.GetConsoleMode(input, out var inputMode) != 0)
                ConsoleApiNative.SetConsoleMode(input, ConsoleApiNative.WithVtInputMode(inputMode));
        }
        catch
        {
            // no console — degraded mode
        }
    }

    private void QuerySize()
    {
        try
        {
            var handle = ConsoleApiNative.GetStdHandle(ConsoleApiNative.StdOutputHandle);
            if (
                handle != -1
                && ConsoleApiNative.GetConsoleScreenBufferInfo(handle, out var info) != 0
            )
            {
                _cols = Math.Max(1, (int)info.Size.X);
                _rows = Math.Max(1, (int)info.Size.Y);
                return;
            }
        }
        catch
        {
            /* fall through to Console fallback */
        }
        try
        {
            _cols = Math.Max(1, Console.WindowWidth);
            _rows = Math.Max(1, Console.WindowHeight);
        }
        catch
        {
            /* 80x24 default */
        }
    }
}
