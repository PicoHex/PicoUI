namespace PicoTui.Loop;

public sealed class UiLoop
{
    private readonly Channel<UiEvent> _channel = Channel.CreateUnbounded<UiEvent>();
    private readonly ITerminal _terminal;
    private readonly ScreenRenderer _screen;
    private IComponent? _root;
    private TimeSpan _frameInterval = TimeSpan.FromMilliseconds(33);
    private DateTime _lastRender = DateTime.MinValue;
    public int RenderCount { get; private set; }

    public UiLoop(ITerminal terminal)
    {
        _terminal = terminal;
        _screen = new ScreenRenderer(terminal);
    }

    public void SetRoot(IComponent root) => _root = root;

    public void SetFrameInterval(TimeSpan t) => _frameInterval = t;

    public void Post(UiEvent e) => _channel.Writer.TryWrite(e);

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var e in _channel.Reader.ReadAllAsync(ct))
            {
                if (e.Kind == EventKind.Resize)
                    _root?.Invalidate();
                MaybeRender();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal shutdown
        }
    }

    private void MaybeRender()
    {
        if (_root is null)
            return;
        var now = DateTime.UtcNow;
        if (now - _lastRender < _frameInterval)
            return;
        _lastRender = now;
        RenderCount++;
        _screen.Render(_root);
    }
}
