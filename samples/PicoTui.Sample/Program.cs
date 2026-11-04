// PicoTui usage sample: a tiny status UI that actually reacts to keys.
// Requires a terminal; exits cleanly when stdin is redirected (CI / pipes).
//
// F10b (F6/F9 residual): this sample used to construct a UiLoop and call
// RunAsync() directly — it never started the terminal, never wrote the alt screen
// and never wired the stdin pump to the loop, so it demonstrated a UI that could
// not receive a single keystroke. It now enters through the production entry point
// (TuiLifecycle.Enter(t, loop)) and exits through TuiLifecycle.ExitAsync(t), which
// is also what releases the reader (Stop() closes stdin, see F10b).
if (Console.IsInputRedirected)
{
    Console.WriteLine("PicoTui.Sample needs a TTY (exit).");
    return;
}

var t = TerminalFactory.Create();
var stack = new VStack();
var lastKey = new Text("last key: (press q or Esc to quit)");
stack.Add(new LayoutItem(new Text("PicoTui — AOT terminal UI")));
stack.Add(new LayoutItem(new Text($"term: {t.GetType().Name}")));
stack.Add(new LayoutItem(lastKey));
var loop = new UiLoop(t);
loop.SetRoot(stack);

using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(30)); // auto-dismiss so the sample cannot hang

// OnKey runs on the loop thread with the decoded key — the handler a component
// keys off. (OnInput still carries the raw unit, e.g. "\x1b[A".)
loop.OnKey = key =>
{
    if (key.Char is 'q' or 'Q' or '\x03' || (key.Char == 'c' && key.Ctrl) || key.Char == Key.Escape.Char)
        cts.Cancel(); // q / Esc / Ctrl-C
    else
        lastKey.SetText($"last key: {Describe(key)}");
};

TuiLifecycle.Enter(t, loop); // Start() + alt screen + byte pump → Key → Post
try
{
    await loop.RunAsync(cts.Token);
}
finally
{
    await TuiLifecycle.ExitAsync(t); // restore the terminal, release stdin
    (t as IDisposable)?.Dispose(); // belt and braces; ExitAsync already did the work
}

Console.WriteLine("bye.");

static string Describe(Key key)
{
    if (key.IsArrowUp)
        return "up";
    if (key.IsArrowDown)
        return "down";
    if (key.IsArrowLeft)
        return "left";
    if (key.IsArrowRight)
        return "right";
    if (key.Char == Key.Enter.Char)
        return "enter";
    var mods = (key.Ctrl ? "ctrl+" : "") + (key.Alt ? "alt+" : "") + (key.Shift ? "shift+" : "");
    return mods.Length > 0 ? $"{mods}{key.Char}" : key.Char >= ' '
        ? key.Char.ToString()
        : $"0x{(int)key.Char:x2}";
}
