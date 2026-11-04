using System.Threading.Channels;

namespace PicoTui.Tests.Terminal;

/// <summary>
/// F10b guard for 04-C2 residual ("<c>Stop()</c> does not close the streams", R3).
/// <para>
/// The F8 surface only required <c>Dispose()</c> to release the reader, but neither
/// PicoAgent nor <c>samples/PicoTui.Sample</c> ever calls <c>Dispose</c> — they call
/// <c>Stop()</c> (through <see cref="TuiLifecycle.ExitAsync"/>). So the blocked
/// <c>ReadAsync</c> stayed pending on an open stdin after the TUI exited: the orphan
/// pump then ate the user's first keystroke in the process that kept running (an
/// agent shell) and only released stdin when the process finally died.
/// </para>
/// <para>
/// These guards fail on the pre-F10b code: <c>Stop()</c> cancelled the token but left
/// the stream open, so the reader never ended and the next released byte was consumed
/// (swallowed) instead of staying for whoever reads stdin next.
/// </para>
/// </summary>
public sealed class Regression_F10_C2_StopReleasesReaderTests
{
    private static Termios Cooked() => Regression_04_C2_RawModeRestoreTests.CookedTermios();

    private static UnixTerminal NewUnix(Stream stdin, Action<string>? onInput = null) =>
        new(
            new FakeTermiosOps(Cooked()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            _ => new StdinPump(stdin, onInput ?? (_ => { }))
        );

    private static async Task<bool> ReaderEnded(StdinPump? pump, int timeoutMs = 2000)
    {
        if (pump is null)
            return false;
        var done = await Task.WhenAny(pump.Completion, Task.Delay(timeoutMs));
        return ReferenceEquals(done, pump.Completion);
    }

    [Test]
    public async Task Stop_ClosesTheInputStream_SoTheBlockedReadCanEnd()
    {
        var stream = new ReleaseGatedStdinStream();
        using var terminal = NewUnix(stream);

        terminal.Start(_ => { }, () => { });
        var pump = terminal.Pump;
        await Task.Delay(50);
        await Assert
            .That(stream.ReadCalls)
            .IsGreaterThanOrEqualTo(1)
            .Because("the read must already be blocked, otherwise Stop() is not being tested");

        terminal.Stop();

        // Stop() — not Dispose() — is the only shutdown every host performs.
        await Assert
            .That(stream.Disposed)
            .IsTrue()
            .Because("Stop() must release the read stream; hosts never call Dispose()");
        await Assert
            .That(await ReaderEnded(pump))
            .IsTrue()
            .Because("an orphan reader that outlives Stop() keeps stdin open");
    }

    [Test]
    public async Task Stop_DoesNotSwallowTheNextKeystroke()
    {
        // The reported symptom: after the TUI exits, the process keeps running (an
        // agent shell / the sample's "bye." prompt). The orphaned pump was still
        // blocked on stdin, so the next keypress was read and discarded by it.
        var stream = new ReleaseGatedStdinStream();
        var units = new List<string>();
        using var terminal = NewUnix(stream, units.Add);

        terminal.Start(units.Add, () => { });
        var pump = terminal.Pump;
        await Task.Delay(50);
        terminal.Stop();
        await Assert.That(await ReaderEnded(pump)).IsTrue();

        var readsBefore = stream.ReadCalls;
        stream.Release((byte)'a'); // the user's next key
        await Task.Delay(100);

        await Assert.That(units.Count).IsEqualTo(0); // never delivered to the TUI
        await Assert
            .That(stream.Consumed)
            .IsEqualTo(0)
            .Because("nobody may consume stdin after Stop(): that byte belongs to the "
                + "next reader, and a stranded read swallowed exactly this keystroke");
        await Assert.That(stream.ReadCalls).IsEqualTo(readsBefore); // no further reads
    }

    [Test]
    public async Task Stop_IsIdempotent_AndDoesNotTouchTheTtyTwice()
    {
        var stream = new ReleaseGatedStdinStream();
        var ops = new FakeTermiosOps(Cooked());
        using var terminal = new UnixTerminal(
            ops,
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(stream, onInput)
        );

        terminal.Start(_ => { }, () => { });
        await Task.Delay(50);

        terminal.Stop();
        var writesAfterFirst = ops.WriteCount;
        await Assert.That(ops.Current).IsEqualTo(Cooked()); // restore already happened

        terminal.Stop(); // must not throw, must not write the tty again
        terminal.Stop();

        await Assert.That(ops.WriteCount).IsEqualTo(writesAfterFirst);
        await Assert.That(terminal.IsRawMode).IsFalse();
        await Assert.That(terminal.Pump).IsNull();
    }

    [Test]
    public async Task ExitAsync_ClosesTheInput_ForHostsThatNeverDispose()
    {
        // The property that matters for PicoAgent and the sample: ExitAsync() alone
        // (no Dispose call) must leave no reader attached to stdin.
        var stream = new ReleaseGatedStdinStream();
        var terminal = NewUnix(stream);

        terminal.Start(_ => { }, () => { });
        var pump = terminal.Pump;
        await Task.Delay(50);

        await TuiLifecycle.ExitAsync(terminal);

        await Assert.That(stream.Disposed).IsTrue();
        await Assert.That(await ReaderEnded(pump)).IsTrue();

        terminal.Dispose();
    }

    [Test]
    public async Task Restart_AfterStop_ReadsWithAFreshReader()
    {
        var first = new ScriptedStdinStream(Encoding.UTF8.GetBytes("a"));
        var second = new ScriptedStdinStream(Encoding.UTF8.GetBytes("b"));
        var streams = new Queue<ScriptedStdinStream>([first, second]);
        var units = new List<string>();
        var terminal = new UnixTerminal(
            new FakeTermiosOps(Cooked()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(streams.Dequeue(), onInput)
        );

        terminal.Start(units.Add, () => { });
        await Assert.That(await ReaderEnded(terminal.Pump)).IsTrue();
        terminal.Stop();
        await Assert.That(first.Disposed).IsTrue();

        terminal.Start(units.Add, () => { });
        await Assert.That(await ReaderEnded(terminal.Pump)).IsTrue();
        terminal.Stop();
        terminal.Dispose();

        await Assert.That(string.Concat(units)).IsEqualTo("ab");
        await Assert.That(second.Disposed).IsTrue();
    }

    [Test]
    public async Task WindowsTerminal_Stop_ClosesTheInputStream()
    {
        var stream = new ReleaseGatedStdinStream();
        var terminal = new WindowsTerminal(
            new FakeConsoleModeOps(),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(stream, onInput)
        );

        terminal.Start(_ => { }, () => { });
        var pump = terminal.Pump;
        await Task.Delay(50);

        terminal.Stop();

        await Assert.That(stream.Disposed).IsTrue();
        await Assert.That(await ReaderEnded(pump)).IsTrue();
        await Assert.That(terminal.IsVtModeActive).IsFalse();
        terminal.Dispose();
    }
}

/// <summary>
/// Models a tty read that ignores cancellation (like a real one) and records whether
/// a byte was consumed by a reader. A reader that survived <c>Stop()</c> shows up as
/// <see cref="Consumed"/> &gt; 0 after a byte is released.
/// </summary>
internal sealed class ReleaseGatedStdinStream : Stream
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();

    internal int ReadCalls;
    internal int Consumed;
    internal bool Disposed { get; private set; }

    internal void Release(params byte[] chunk) => _chunks.Writer.TryWrite(chunk);

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    )
    {
        Interlocked.Increment(ref ReadCalls);
        if (Disposed)
            return 0;
        // deliberately CancellationToken.None: a tty read only ends on a byte or close
        if (!await _chunks.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
            return 0;
        if (!_chunks.Reader.TryRead(out var chunk))
            return 0;
        Interlocked.Increment(ref Consumed);
        var count = Math.Min(chunk.Length, buffer.Length);
        chunk.AsSpan(0, count).CopyTo(buffer.Span);
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        _chunks.Writer.TryComplete(); // closing unblocks the pending read with 0
        base.Dispose(disposing);
    }
}
