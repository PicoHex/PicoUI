namespace PicoTui.Tests.Loop;

/// <summary>
/// WP-F5 guard for 04-C2 (Critical): <c>TuiLifecycle.ExitAsync</c> wrote 7 escape
/// sequences and drained input <em>before</em> calling <c>Stop()</c> with no
/// try/finally — a broken stdout (EPIPE) skipped the restore and left the user's
/// shell in raw mode.
/// </summary>
public sealed class Regression_04_C2_ExitRestoreTests
{
    [Test]
    public async Task ExitAsync_StopRunsWhenWriteThrows()
    {
        var terminal = new RecordingTerminal { ThrowOnWrite = true };

        var threw = false;
        try
        {
            await TuiLifecycle.ExitAsync(terminal);
        }
        catch (IOException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue(); // the failure still surfaces
        await Assert.That(terminal.StopCount).IsEqualTo(1);
    }

    [Test]
    public async Task ExitAsync_StopRunsOnSuccess()
    {
        var terminal = new RecordingTerminal();
        await TuiLifecycle.ExitAsync(terminal);
        await Assert.That(terminal.StopCount).IsEqualTo(1);
    }

    [Test]
    public async Task ExitAsync_IsIdempotent()
    {
        var terminal = new RecordingTerminal();
        await TuiLifecycle.ExitAsync(terminal);
        await TuiLifecycle.ExitAsync(terminal);
        await Assert.That(terminal.StopCount).IsEqualTo(2); // no throw, no double-restore
    }

    private sealed class RecordingTerminal : ITerminal
    {
        public bool ThrowOnWrite { get; init; }
        public int StopCount { get; private set; }
        public string Output { get; private set; } = "";

        public int Columns => 80;
        public int Rows => 24;

        public void Start(Action<string> onInput, Action onResize) { }

        public void Stop() => StopCount++;

        public void Write(string data)
        {
            if (ThrowOnWrite)
                throw new IOException("EPIPE");
            Output += data;
        }

        public void MoveBy(int lines) => Write($"\x1b[{lines}A");

        public void HideCursor() => Write("\x1b[?25l");

        public void ShowCursor() => Write("\x1b[?25h");

        public void ClearLine() => Write("\x1b[2K");

        public void ClearFromCursor() => Write("\x1b[0K");

        public void ClearScreen() => Write("\x1b[2J");

        public void SetTitle(string title) => Write(title);

        public Task DrainInputAsync(int maxMs, int idleMs) => Task.CompletedTask;
    }
}
