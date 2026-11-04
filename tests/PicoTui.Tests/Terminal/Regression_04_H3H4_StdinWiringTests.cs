using System.Threading.Channels;

namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F8 guards for 04-H3 ≡ 06-H2 / 04-H4 <em>at the wiring level</em>: a terminal
/// must read stdin through <see cref="StdinPump"/> (EOF ends the loop instead of
/// spinning a core, one callback per input unit instead of one per 4096-byte
/// block), <c>Stop()</c> must return promptly without writing to the tty
/// afterwards, and <c>Dispose()</c> must release the reader idempotently.
/// <para>
/// The old production loop (<c>UnixTerminal.cs:82-97</c> /
/// <c>WindowsTerminal.cs:59-72</c> before WP-F8) re-opened
/// <c>Console.OpenStandardInput()</c> on every iteration, had no <c>n == 0</c>
/// break, decoded nothing incrementally and handed whole 4 KB blocks to
/// <c>onInput</c>; a restarted terminal also kept the previous reader alive.
/// </para>
/// </summary>
public sealed class Regression_04_H3H4_StdinWiringTests
{
    private static Termios Cooked() => Regression_04_C2_RawModeRestoreTests.CookedTermios();

    /// <summary>Terminal whose stdin reader gets its stream from the factory.</summary>
    private static UnixTerminal NewUnix(Stream stdin, List<string>? sink = null)
    {
        sink ??= [];
        return new UnixTerminal(
            new FakeTermiosOps(Cooked()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(stdin, onInput)
        );
    }

    private static async Task WaitForReader(StdinPump? pump, int timeoutMs = 3000)
    {
        await Assert.That(pump).IsNotNull().Because("Start() must wire a StdinPump");
        var done = await Task.WhenAny(pump!.Completion, Task.Delay(timeoutMs));
        await Assert
            .That(ReferenceEquals(done, pump.Completion))
            .IsTrue()
            .Because("the stdin reader never stopped (EOF / shutdown ignored, so it spins)");
        await pump.Completion;
    }

    /// <summary>Waits for the loop to end without rethrowing: the hostile-stream
    /// guard expects a faulted task, only a hang must fail.</summary>
    private static async Task WaitForReaderEnd(StdinPump? pump, int timeoutMs = 3000)
    {
        await Assert.That(pump).IsNotNull().Because("Start() must wire a StdinPump");
        var done = await Task.WhenAny(pump!.Completion, Task.Delay(timeoutMs));
        await Assert
            .That(ReferenceEquals(done, pump.Completion))
            .IsTrue()
            .Because("the stdin reader never stopped");
    }

    [Test]
    public async Task Eof_StopsTheReaderAtTheFirstRead()
    {
        // the old loop called Read again and again after EOF → one core pegged
        var stream = new ScriptedStdinStream(); // immediately 0 bytes
        using var terminal = NewUnix(stream);

        terminal.Start(_ => { }, () => { });
        await WaitForReader(terminal.Pump, 2000);

        await Assert.That(terminal.Pump!.ReadCount).IsEqualTo(1); // read once, then stop
        await Assert.That(stream.Disposed).IsTrue(); // the pump owns one stream per session
    }

    [Test]
    public async Task OneBlockOfThreeKeys_IsDeliveredAsThreeUnits()
    {
        // the old loop delivered the whole 4096-byte block as ONE callback
        var units = new List<string>();
        using var terminal = NewUnix(
            new ScriptedStdinStream(Encoding.UTF8.GetBytes("abc")),
            units
        );

        terminal.Start(units.Add, () => { });
        await WaitForReader(terminal.Pump);

        await Assert.That(string.Concat(units)).IsEqualTo("abc");
        await Assert.That(units.Count).IsEqualTo(3); // a, b, c — not "abc"
    }

    [Test]
    public async Task EscapeSequence_ArrivesAsOneUnit()
    {
        var units = new List<string>();
        using var terminal = NewUnix(
            new ScriptedStdinStream(Encoding.UTF8.GetBytes("a\x1b[Ab")),
            units
        );

        terminal.Start(units.Add, () => { });
        await WaitForReader(terminal.Pump);

        await Assert.That(units.Count).IsEqualTo(3);
        await Assert.That(units[1]).IsEqualTo("\x1b[A"); // arrow-up, not ESC + "[" + "A"
    }

    [Test]
    public async Task BracketedPasteBody_IsOneUnit()
    {
        // TuiLifecycle.Enter() writes ?2004h right after Start(), so the framing
        // buffer must already recognise a paste body when the first byte arrives.
        var units = new List<string>();
        using var terminal = NewUnix(
            new ScriptedStdinStream(Encoding.UTF8.GetBytes("\x1b[200~ab\x1b[201~")),
            units
        );

        terminal.Start(units.Add, () => { });
        await WaitForReader(terminal.Pump);

        await Assert.That(units.Count).IsEqualTo(1);
        await Assert.That(units[0]).IsEqualTo("\x1b[200~ab\x1b[201~");
    }

    [Test]
    public async Task Stop_ReturnsPromptly_AndStillRestoresTheTty()
    {
        // a blocked tty read cannot be interrupted portably: Stop() must not join it
        var stream = new GatedStdinStream();
        var ops = new FakeTermiosOps(Cooked());
        var terminal = new UnixTerminal(
            ops,
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(stream, onInput)
        );

        terminal.Start(_ => { }, () => { });
        var pump = terminal.Pump;
        await Task.Delay(50); // let the read block
        await Assert.That(terminal.IsRawMode).IsTrue();
        await Assert
            .That(stream.ReadCalls)
            .IsGreaterThanOrEqualTo(1)
            .Because("a read must be in flight, otherwise Stop() is not being tested");

        var sw = Stopwatch.StartNew();
        terminal.Stop();
        sw.Stop();

        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(500); // no join on the read
        await Assert.That(terminal.IsRawMode).IsFalse(); // restore still happens
        await Assert.That(ops.Current).IsEqualTo(Cooked()); // and it is the snapshot
        await Assert.That(ops.ReadCount).IsEqualTo(1); // Restore never re-reads the tty

        terminal.Dispose(); // releases the stream → the blocked read returns 0
        await WaitForReader(pump, 2000);
    }

    [Test]
    public async Task InputArrivingAfterStop_IsNeverDelivered()
    {
        // the last word on the tty state belongs to Stop(): a byte that arrives
        // while the read is still blocked must NOT reach onInput afterwards
        var stream = new GatedStdinStream();
        var units = new List<string>();
        using var terminal = new UnixTerminal(
            new FakeTermiosOps(Cooked()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(stream, onInput)
        );

        terminal.Start(units.Add, () => { });
        var pump = terminal.Pump;
        await Task.Delay(50);
        await Assert
            .That(stream.ReadCalls)
            .IsGreaterThanOrEqualTo(1)
            .Because("the read must already be blocked when Stop() runs");
        terminal.Stop();

        stream.Release((byte)'a'); // the read now completes *after* Stop()
        await WaitForReader(pump, 2000);

        await Assert.That(units.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_EndsTheReader_AndIsIdempotent()
    {
        var stream = new GatedStdinStream();
        var terminal = NewUnix(stream);

        terminal.Start(_ => { }, () => { });
        var pump = terminal.Pump;
        await Task.Delay(50);
        await Assert
            .That(stream.ReadCalls)
            .IsGreaterThanOrEqualTo(1)
            .Because("Dispose() must release a reader that is actually reading");

        terminal.Dispose();
        terminal.Dispose(); // second Dispose must not throw

        await WaitForReader(pump, 2000);
        await Assert.That(stream.Disposed).IsTrue();
        await Assert.That(terminal.Pump).IsNull(); // the reader reference is released
    }

    [Test]
    public async Task Restart_AfterDispose_ReadsWithAFreshReader()
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
        await WaitForReader(terminal.Pump);
        terminal.Dispose();
        await Assert.That(first.Disposed).IsTrue(); // the old reader is gone, not leaked

        terminal.Start(units.Add, () => { });
        await WaitForReader(terminal.Pump);
        terminal.Dispose();

        await Assert.That(string.Concat(units)).IsEqualTo("ab");
        await Assert.That(second.Disposed).IsTrue();
    }

    [Test]
    public async Task ReadFailure_EndsTheReader_AndStopStillRestores()
    {
        var ops = new FakeTermiosOps(Cooked());
        var terminal = new UnixTerminal(
            ops,
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(new FailingStdinStream(), onInput)
        );

        terminal.Start(_ => { }, () => { });
        await WaitForReader(terminal.Pump, 2000); // a hung-up tty must not spin
        await Assert.That(terminal.Pump!.ReadCount).IsEqualTo(1);

        terminal.Stop(); // restore is not conditional on a healthy reader
        await Assert.That(terminal.IsRawMode).IsFalse();
        await Assert.That(ops.Current).IsEqualTo(Cooked());
        terminal.Dispose();
    }

    [Test]
    public async Task UnexpectedReadException_IsObservedOnTheTerminal_AndStopStillRestores()
    {
        // the old code discarded its reader Task, so a fault surfaced only as an
        // UnobservedTaskException at finalisation — nobody could see it
        var ops = new FakeTermiosOps(Cooked());
        var terminal = new UnixTerminal(
            ops,
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(new HostileStdinStream(), onInput)
        );

        terminal.Start(_ => { }, () => { });
        await WaitForReaderEnd(terminal.Pump, 2000); // the loop ends, it does not spin

        var deadline = Stopwatch.StartNew();
        while (terminal.ReaderFault is null && deadline.ElapsedMilliseconds < 2000)
            await Task.Delay(10);

        await Assert
            .That(terminal.ReaderFault)
            .IsNotNull()
            .Because("the reader task must be observed, not fire-and-forget");

        terminal.Stop();
        await Assert.That(terminal.IsRawMode).IsFalse();
        await Assert.That(ops.Current).IsEqualTo(Cooked());
        terminal.Dispose();
    }

    [Test]
    public async Task WithoutSystemHooks_NoReaderIsStarted()
    {
        // the fake-hooks mode used by the other terminal guards must not touch
        // the test process's real stdin
        using var terminal = new UnixTerminal(
            new FakeTermiosOps(Cooked()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false,
            _ => new StdinPump(new ScriptedStdinStream(), _ => { })
        );

        terminal.Start(_ => { }, () => { });

        await Assert.That(terminal.Pump).IsNull();
    }

    [Test]
    public async Task WindowsTerminal_ReadsThroughTheSamePump()
    {
        var units = new List<string>();
        var stream = new ScriptedStdinStream(Encoding.UTF8.GetBytes("abc"));
        var terminal = new WindowsTerminal(
            new FakeConsoleModeOps(),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            onInput => new StdinPump(stream, onInput)
        );

        terminal.Start(units.Add, () => { });
        await WaitForReader(terminal.Pump);

        await Assert.That(terminal.Pump!.ReadCount).IsEqualTo(2); // data read + EOF read, then stop
        await Assert.That(units.Count).IsEqualTo(3); // one callback per unit
        await Assert.That(stream.Disposed).IsTrue();

        terminal.Stop();
        await Assert.That(terminal.IsVtModeActive).IsFalse(); // modes still restored
        terminal.Dispose();
    }
}

/// <summary>Replays byte chunks, then reports EOF forever.</summary>
internal sealed class ScriptedStdinStream(params byte[][] chunks) : Stream
{
    private int _next;

    internal bool Disposed { get; private set; }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(Span<byte> buffer)
    {
        if (_next >= chunks.Length)
            return 0; // EOF
        var chunk = chunks[_next++];
        var count = Math.Min(chunk.Length, buffer.Length);
        chunk.AsSpan(0, count).CopyTo(buffer);
        return count;
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => ValueTask.FromResult(Read(buffer.Span));

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

/// <summary>
/// Models an idle tty: a read blocks until a byte is released or the stream is
/// closed, and — like a real terminal read — it ignores cancellation.
/// </summary>
internal sealed class GatedStdinStream : Stream
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();

    internal int ReadCalls;

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
        // deliberately CancellationToken.None: a tty read only ends on a byte or close
        if (!await _chunks.Reader.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
            return 0;
        if (!_chunks.Reader.TryRead(out var chunk))
            return 0;
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
        _chunks.Writer.TryComplete(); // shutdown unblocks the pending read with 0
        base.Dispose(disposing);
    }
}

/// <summary>Models a hung-up tty: the read throws instead of returning bytes.</summary>
internal sealed class FailingStdinStream : Stream
{    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(Span<byte> buffer) => throw new IOException("tty hung up");

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => ValueTask.FromException<int>(new IOException("tty hung up"));

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}

/// <summary>A read that fails for a reason the pump does not swallow (not an
/// IOException): the reader task faults instead of ending quietly.</summary>
internal sealed class HostileStdinStream : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(Span<byte> buffer) =>
        throw new InvalidOperationException("reader blew up");

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default
    ) => ValueTask.FromException<int>(new InvalidOperationException("reader blew up"));

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
