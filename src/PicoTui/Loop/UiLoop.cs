namespace PicoTui.Loop;

public sealed class UiLoop
{
    private readonly Channel<UiEvent> _channel = Channel.CreateUnbounded<UiEvent>();
    private readonly ScreenRenderer _screen;
    private IComponent? _root;
    private TimeSpan _frameInterval = TimeSpan.FromMilliseconds(33);
    private long _lastRenderStamp = long.MinValue;
    private volatile bool _dirty;

    /// <summary>Frame-deadline timestamp source (monotonic by default; test seam).</summary>
    internal Func<long> TimestampProvider { get; set; } = Stopwatch.GetTimestamp;

    public int RenderCount { get; private set; }

    public UiLoop(ITerminal terminal)
    {
        _screen = new ScreenRenderer(terminal);
    }

    public void SetRoot(IComponent root)
    {
        _root = root;
        RequestRender();
    }

    public void SetFrameInterval(TimeSpan t) => _frameInterval = t;

    public void Post(UiEvent e)
    {
        _dirty = true;
        _channel.Writer.TryWrite(e);
    }

    /// <summary>
    /// Requests an early frame for state changed outside <see cref="Post"/>.
    /// Optional: the frame timer already commits every frame, so this only
    /// shortens the latency of the next commit.
    /// </summary>
    public void RequestRender()
    {
        _dirty = true;
        _channel.Writer.TryWrite(new UiEvent(EventKind.Tick));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            MaybeRender(force: true); // initial frame — rendering must not wait for input
            while (true)
            {
                var wake = await WaitForWorkAsync(ct);
                if (wake == WakeReason.Closed)
                    break;
                // drain a batch: high-frequency events coalesce into one frame
                while (_channel.Reader.TryRead(out var e))
                {
                    if (e.Kind == EventKind.Resize)
                        _root?.Invalidate();
                }
                // the wake reason decides the commit: the elapsed-time check is only a
                // guard for early event wakes (a frozen/jumping clock cannot stall it)
                MaybeRender(deadlineElapsed: wake == WakeReason.Deadline);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // normal shutdown
        }
    }

    private enum WakeReason
    {
        Event,
        Deadline,
        Closed,
    }

    /// <summary>
    /// Waits for an event, or — when a frame interval is active — for the next
    /// frame deadline. The deadline makes the loop self-healing: state a
    /// component reads is committed within one frame even if nobody notified
    /// the loop (no Post/RequestRender). Interval 0 = on-demand only.
    /// </summary>
    private async Task<WakeReason> WaitForWorkAsync(CancellationToken ct)
    {
        if (_frameInterval <= TimeSpan.Zero)
            return await _channel.Reader.WaitToReadAsync(ct) ? WakeReason.Event : WakeReason.Closed;
        var remaining = _frameInterval - SinceLastRender();
        if (remaining <= TimeSpan.Zero)
            return WakeReason.Deadline;
        try
        {
            return await _channel.Reader.WaitToReadAsync(ct).AsTask().WaitAsync(remaining, ct)
                ? WakeReason.Event
                : WakeReason.Closed;
        }
        catch (TimeoutException)
        {
            return WakeReason.Deadline;
        }
    }

    private void MaybeRender(bool force = false, bool deadlineElapsed = false)
    {
        if (_root is null)
        {
            _dirty = false; // nothing to commit; avoids a spin until a root is set
            return;
        }
        if (!force && !deadlineElapsed && _frameInterval <= TimeSpan.Zero && !_dirty)
            return; // on-demand mode: only explicit requests/events render
        if (!force && !deadlineElapsed && SinceLastRender() < _frameInterval)
            return; // early event; the frame deadline commits the pending state
        _lastRenderStamp = TimestampProvider();
        _dirty = false;
        RenderCount++;
        _screen.Render(_root);
    }

    private TimeSpan SinceLastRender()
    {
        if (_lastRenderStamp == long.MinValue)
            return TimeSpan.MaxValue;
        var delta = TimestampProvider() - _lastRenderStamp;
        // a non-monotonic or jumping source must never push the deadline further out
        return delta <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(delta / (double)Stopwatch.Frequency);
    }
}
