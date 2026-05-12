using PicoTUI.Events;
using PicoTUI.Widgets;

namespace PicoTUI;

/// <summary>
/// Main entry point for a PicoTUI application.
/// Manages the event loop, rendering pipeline, and widget tree.
/// </summary>
public sealed class Application : IDisposable
{
    private readonly Renderer _renderer;
    private readonly List<Widget> _widgets = [];
    private bool _running;
    private bool _disposed;

    /// <summary>
    /// Creates a new application. Sets up the terminal (alternate screen, raw mode,
    /// hidden cursor) and prepares the renderer.
    /// </summary>
    public Application()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Terminal.EnterAlternateScreen();
        Terminal.HideCursor();
        Terminal.EnableRawMode();
        _renderer = new Renderer();
    }

    // ── Widget management ─────────────────────────────────────────────────────

    /// <summary>Adds a widget to the root widget tree.</summary>
    public Application Add(Widget widget) { _widgets.Add(widget); return this; }

    /// <summary>Removes all widgets.</summary>
    public Application ClearWidgets() { _widgets.Clear(); return this; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called every frame after all widgets have been rendered.
    /// Receives the next input event (or <see langword="null"/> on timeout).
    /// Return <see langword="false"/> to exit the event loop.
    /// </summary>
    public Func<Event?, bool>? OnEvent { get; set; }

    /// <summary>
    /// Called every frame before rendering.  Useful for updating widget state.
    /// The <see cref="Canvas"/> passed in is the one widgets will draw into.
    /// </summary>
    public Action<Canvas>? OnRender { get; set; }

    // ── Event loop ────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the application until <see cref="Quit"/> is called or
    /// <see cref="OnEvent"/> returns <see langword="false"/>.
    /// </summary>
    public void Run()
    {
        _running = true;

        // Initial resize
        var sz = Terminal.GetSize();
        _renderer.Resize(sz.Width, sz.Height);

        while (_running)
        {
            // Allow caller to update state / widget tree
            OnRender?.Invoke(_renderer.Canvas);

            // Render all root widgets
            foreach (var widget in _widgets)
                if (widget.IsVisible)
                    widget.Render(_renderer.Canvas);

            _renderer.Render();

            // Read next event (may block briefly)
            Event? ev = null;
            if (Console.KeyAvailable)
                ev = InputReader.ReadEvent();

            if (ev is ResizeEvent resize)
                _renderer.Resize(resize.Size.Width, resize.Size.Height);

            bool cont = OnEvent?.Invoke(ev) ?? true;
            if (!cont) _running = false;
        }
    }

    /// <summary>Requests the event loop to stop after the current frame.</summary>
    public void Quit() => _running = false;

    // ── IDisposable ────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _renderer.Dispose();
        Terminal.DisableRawMode();
        Terminal.ShowCursor();
        Terminal.LeaveAlternateScreen();
    }
}
