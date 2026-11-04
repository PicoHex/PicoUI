namespace PicoTui.Tests.Input;

/// <summary>
/// 04-H3 ≡ 06-H2 / 04-H4 guards: the stdin reader must stop at EOF (the old
/// loop kept calling Read and burned a whole core), must dispose the stream it
/// opens, must return from Stop() promptly, and must deliver ONE callback per
/// input unit (StdinBuffer framing) instead of one callback per 4096-byte block.
/// </summary>
public sealed class Regression_04_H3H4_StdinPumpTests
{
    /// <summary>Stream that replays byte chunks and then reports EOF.</summary>
    private sealed class ScriptedStream(params byte[][] chunks) : Stream
    {
        private int _next;
        public bool Disposed { get; private set; }

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
            chunk.AsSpan(0, Math.Min(chunk.Length, buffer.Length)).CopyTo(buffer);
            return Math.Min(chunk.Length, buffer.Length);
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

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

    /// <summary>Stream whose read never completes until it is disposed —
    /// models a terminal with no pending input.</summary>
    private sealed class BlockingStream : Stream
    {
        private readonly TaskCompletionSource<int> _read = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(Span<byte> buffer) => _read.Task.GetAwaiter().GetResult();

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        ) =>
            new(
                _read.Task.ContinueWith(
                    t => t.Result,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default
                )
            );

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _read.TrySetResult(0); // shutdown unblocks the pending read
            base.Dispose(disposing);
        }
    }

    /// <summary>Waits for the pump to stop, failing (instead of hanging) when it
    /// does not.</summary>
    private static async Task AssertStopped(StdinPump pump, int timeoutMs = 3000)
    {
        var done = await Task.WhenAny(pump.Completion, Task.Delay(timeoutMs));
        await Assert
            .That(ReferenceEquals(done, pump.Completion))
            .IsTrue()
            .Because("the read loop did not stop (EOF / shutdown ignored)");
        await pump.Completion;
    }

    private static async Task<List<string>> RunToCompletion(params byte[][] chunks)
    {
        var units = new List<string>();
        using var pump = new StdinPump(new ScriptedStream(chunks), units.Add);
        pump.Start();
        await AssertStopped(pump);
        return units;
    }

    [Test]
    public async Task SingleBlockOfThreeCharacters_IsDeliveredAsThreeUnits()
    {
        var units = await RunToCompletion([Encoding.UTF8.GetBytes("abc")]);

        await Assert.That(units.Count).IsEqualTo(3); // not one 3-character block
        await Assert.That(units[0]).IsEqualTo("a");
        await Assert.That(units[1]).IsEqualTo("b");
        await Assert.That(units[2]).IsEqualTo("c");
    }

    [Test]
    public async Task EscapeSequence_IsDeliveredAsOneUnit()
    {
        // "ESC [ A" must arrive as a single arrow-up unit, not as ESC, "[", "A"
        var units = await RunToCompletion([Encoding.UTF8.GetBytes("a\x1b[Ab")]);

        await Assert.That(units.Count).IsEqualTo(3);
        await Assert.That(units[1]).IsEqualTo("\x1b[A");
    }

    [Test]
    public async Task RuneSplitAcrossBlocks_IsDeliveredOnce()
    {
        var bytes = Encoding.UTF8.GetBytes("😀");
        var units = await RunToCompletion(
            [bytes[..2], bytes[2..]] // 4-byte rune split over two reads
        );

        await Assert.That(units.Count).IsEqualTo(1);
        await Assert.That(units[0]).IsEqualTo("😀");
    }

    [Test]
    public async Task Eof_StopsTheLoopInsteadOfSpinning()
    {
        var stream = new ScriptedStream(); // immediately 0 bytes
        using var pump = new StdinPump(stream, _ => { });
        pump.Start();

        await AssertStopped(pump, 2000);
        await Assert.That(pump.ReadCount).IsEqualTo(1); // read once, then break — no spin
        await Assert.That(stream.Disposed).IsTrue(); // the stream the pump owns is released
    }

    [Test]
    public async Task Eof_DoesNotInvokeTheHandler()
    {
        var calls = 0;
        using var pump = new StdinPump(new ScriptedStream(), _ => calls++);
        pump.Start();
        await AssertStopped(pump, 2000);

        await Assert.That(calls).IsEqualTo(0);
    }

    [Test]
    public async Task Stop_ReturnsPromptlyWhileAReadIsBlocked()
    {
        var stream = new BlockingStream();
        var pump = new StdinPump(stream, _ => { });
        pump.Start();
        await Task.Delay(50); // let the read block

        var sw = Stopwatch.StartNew();
        pump.Stop();
        sw.Stop();

        await Assert.That(sw.ElapsedMilliseconds).IsLessThan(500); // no join on the blocked read

        pump.Dispose(); // releases the stream → the read returns 0
        await AssertStopped(pump, 2000);
    }

    [Test]
    public async Task CallbackFailure_DoesNotKillTheRestOfTheInput()
    {
        var units = new List<string>();
        using var pump = new StdinPump(
            new ScriptedStream(Encoding.UTF8.GetBytes("abc")),
            u =>
            {
                if (u == "b")
                    throw new InvalidOperationException("boom");
                units.Add(u);
            }
        );
        pump.Start();
        await AssertStopped(pump, 2000);

        await Assert.That(units.Count).IsEqualTo(2); // "a" and "c" still delivered
        await Assert.That(pump.CallbackFailures).IsEqualTo(1);
    }

    [Test]
    public async Task StartIsIdempotent()
    {
        var units = new List<string>();
        using var pump = new StdinPump(new ScriptedStream(Encoding.UTF8.GetBytes("a")), units.Add);

        pump.Start();
        pump.Start(); // must not spawn a second reader

        await AssertStopped(pump, 2000);
        await Assert.That(units.Count).IsEqualTo(1);
    }
}
