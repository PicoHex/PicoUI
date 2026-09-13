// PicoTui usage sample: a tiny status UI. Requires a terminal; exits cleanly
// when stdin is redirected (CI / pipes). Dismisses itself after 3 seconds.
if (Console.IsInputRedirected)
{
    Console.WriteLine("PicoTui.Sample needs a TTY (exit).");
    return;
}

var t = TerminalFactory.Create();
var stack = new VStack();
stack.Add(new LayoutItem(new Text("PicoTui — AOT terminal UI")));
stack.Add(new LayoutItem(new Text($"term: {t.GetType().Name}")));
stack.Add(new LayoutItem(new Text("will dismiss in 3 seconds")));
var loop = new UiLoop(t);
loop.SetRoot(stack);

using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(3));
await loop.RunAsync(cts.Token);
await TuiLifecycle.ExitAsync(t);
Console.WriteLine("bye.");
