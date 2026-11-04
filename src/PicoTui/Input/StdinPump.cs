namespace PicoTui.Input;

/// <summary>
/// Owns the stdin read loop: reads byte blocks, frames them into input units
/// with <see cref="StdinBuffer"/> and delivers ONE callback per unit.
/// </summary>
/// <remarks>
/// <para>04-H3 ≡ 06-H2: the previous loop lived inside the terminal and re-opened
/// the standard-input stream on every iteration, never broke on <c>n == 0</c> (so
/// EOF — <c>&lt;/dev/null</c>, a closed ssh session, a dead parent pipe — spun a
/// whole core forever), could not be cancelled while a read was blocked and
/// dropped the managed <see cref="Stream"/> objects it created on the floor.</para>
/// <para>04-H4: it also delivered whatever <c>Read</c> returned — usually a
/// 4096-byte block — as a single <c>onInput</c> callback. Consumers only react to
/// one-unit sequences, so fast typing and paste dropped whole blocks and
/// <c>Home</c>/<c>F1</c>-<c>F12</c> were never recognised. <see cref="StdinBuffer"/>
/// frames the byte stream instead, and every drained unit gets its own callback.</para>
/// <para>Shutdown: <see cref="Stop"/> only requests cancellation and returns — a
/// blocking <c>Read</c> on a tty cannot be interrupted portably, so the read only
/// finishes when a byte arrives or the stream is closed (see
/// <see cref="Completion"/>). <see cref="Dispose"/> closes the stream it owns,
/// which unblocks such a read.</para>
/// </remarks>
public sealed class StdinPump : IDisposable
{
    private const int BlockSize = 4096;

    private readonly Stream _input;
    private readonly Action<string> _onInput;
    private readonly StdinBuffer _buffer = new();
    private readonly bool _ownsStream;
    private readonly CancellationTokenSource _cts = new();

    private Task? _completion;
    private long _readCount;
    private long _callbackFailures;
    private bool _disposed;

    /// <param name="input">Byte source to read from (usually standard input).</param>
    /// <param name="onInput">Invoked once per decoded input unit (never per
    /// read block); exceptions thrown here are counted, not propagated, so one
    /// bad key cannot kill the input path.</param>
    /// <param name="ownsStream">When true (the default) the pump disposes
    /// <paramref name="input"/> when the loop ends or the pump is disposed.</param>
    public StdinPump(Stream input, Action<string> onInput, bool ownsStream = true)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _onInput = onInput ?? throw new ArgumentNullException(nameof(onInput));
        _ownsStream = ownsStream;
    }

    /// <summary>Pump over the process's standard input (one stream instance for
    /// the whole session — not one per read).</summary>
    public static StdinPump ForConsole(Action<string> onInput) =>
        new(Console.OpenStandardInput(), onInput, ownsStream: true);

    /// <summary>Number of <c>Read</c> attempts made (a guard surface: an EOF loop
    /// that does not break shows up here as an ever-growing count). Counted before
    /// the read, so a read that throws still counts as one attempt.</summary>
    public long ReadCount => Interlocked.Read(ref _readCount);

    /// <summary>Number of <c>onInput</c> callbacks that threw.</summary>
    public long CallbackFailures => Interlocked.Read(ref _callbackFailures);

    /// <summary>Framing buffer (exposed so a terminal can toggle paste mode).</summary>
    public StdinBuffer Buffer => _buffer;

    /// <summary>True once the read loop has finished (EOF, error or shutdown).</summary>
    public bool IsCompleted => _completion is { IsCompleted: true };

    /// <summary>Completes when the read loop ends.</summary>
    public Task Completion => _completion ?? Task.CompletedTask;

    /// <summary>Starts the read loop on the thread pool. Idempotent.</summary>
    public void Start()
    {
        if (_completion is not null)
            return;
        // the token is not passed to Task.Run: a shutdown before the task gets
        // scheduled must still run the loop's finally block
        _completion = Task.Run(() => RunAsync(_cts.Token));
    }

    /// <summary>
    /// Reads until EOF, a read error or cancellation. Exposed so a caller can run
    /// the loop on its own (already cancelling) thread.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        var buf = new byte[BlockSize];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int n;
                Interlocked.Increment(ref _readCount);
                try
                {
                    n = await _input.ReadAsync(buf.AsMemory(0, buf.Length), ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break; // shutdown
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    break; // tty hung up / stream closed under us
                }

                if (n <= 0)
                    break; // 04-H3: EOF must end the loop — a bare continue spins a core

                // WP-F8: a read that was already blocked when Stop() ran returns bytes
                // *after* the terminal restored raw mode. Delivering them would let a
                // consumer write to the tty past restore, so shutdown wins and the
                // pending bytes are dropped (F5 keeps the restore as the last word).
                if (ct.IsCancellationRequested)
                    break;

                Deliver(buf, n);
            }
        }
        finally
        {
            if (_ownsStream)
            {
                try
                {
                    _input.Dispose();
                }
                catch
                {
                    /* shutdown must not throw */
                }
            }
        }
    }

    /// <summary>Requests shutdown; returns immediately (see type remarks).</summary>
    public void Stop()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            /* already disposed */
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Stop();
        if (_ownsStream)
        {
            try
            {
                _input.Dispose(); // unblocks a read that is waiting for a byte
            }
            catch
            {
                /* shutting down */
            }
        }
        _cts.Dispose();
    }

    private void Deliver(byte[] buf, int count)
    {
        _buffer.Append(buf.AsSpan(0, count));
        foreach (var unit in _buffer.Drain())
        {
            try
            {
                _onInput(unit);
            }
            catch
            {
                // 04-H3: an exception used to vanish into a discarded Task; count it
                // and keep the rest of the decoded units flowing.
                Interlocked.Increment(ref _callbackFailures);
            }
        }
    }
}
