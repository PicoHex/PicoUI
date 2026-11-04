namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-C2 (Critical): raw mode must be entered through a session that
/// <em>saves</em> the original termios and <em>writes that snapshot back</em>, instead
/// of the old "tcgetattr + no transform" no-op that left the user's shell in raw mode.
/// Uses a fake termios surface, so it runs on every platform.
/// </summary>
public sealed class Regression_04_C2_RawModeRestoreTests
{
    private const uint Isig = 0x1;
    private const uint Icanon = 0x2;
    private const uint Echo = 0x8;
    private const uint Iexten = 0x8000;
    private const uint Icrnl = 0x100;
    private const uint Ixon = 0x400;
    private const uint Opost = 0x1;
    private const uint Csize = 0x30;
    private const uint Parenb = 0x100;

    [Test]
    public async Task Enter_ThenRestore_RestoresCanonEchoIsig()
    {
        var ops = new FakeTermiosOps(CookedTermios());
        var session = new RawModeSession(ops);

        await Assert.That(session.Enter()).IsTrue();
        await Assert.That(session.IsRaw).IsTrue();

        // raw really applied
        var raw = ops.Current;
        await Assert.That(raw.c_lflag & (Icanon | Echo | Isig)).IsEqualTo(0u);
        await Assert.That(raw.c_iflag & Icrnl).IsEqualTo(0u);
        await Assert.That(raw.c_oflag & Opost).IsEqualTo(0u);
        await Assert.That(raw.c_cflag & Csize).IsEqualTo(Csize);

        await Assert.That(session.Restore()).IsTrue();
        await Assert.That(session.IsRaw).IsFalse();

        // cooked bits are back — exactly the snapshot, not a re-read
        var restored = ops.Current;
        await Assert.That(restored.c_lflag & (Icanon | Echo | Isig | Iexten)).IsNotEqualTo(0u);
        await Assert.That(restored.c_lflag & Icanon).IsEqualTo(Icanon);
        await Assert.That(restored.c_lflag & Echo).IsEqualTo(Echo);
        await Assert.That(restored.c_lflag & Isig).IsEqualTo(Isig);
        await Assert.That(restored.c_iflag & Icrnl).IsEqualTo(Icrnl);
        await Assert.That(restored).IsEqualTo(CookedTermios());
    }

    [Test]
    public async Task Restore_DoesNotReReadTheTty()
    {
        // The old bug: SetRawMode(false) did tcgetattr and wrote the *raw* values back.
        var ops = new FakeTermiosOps(CookedTermios());
        var session = new RawModeSession(ops);
        session.Enter();
        var readsAfterEnter = ops.ReadCount;

        session.Restore();

        await Assert.That(ops.ReadCount).IsEqualTo(readsAfterEnter);
    }

    [Test]
    public async Task Restore_IsIdempotent()
    {
        var ops = new FakeTermiosOps(CookedTermios());
        var session = new RawModeSession(ops);
        session.Enter();
        var writesAfterEnter = ops.WriteCount;

        await Assert.That(session.Restore()).IsTrue();
        var writesAfterRestore = ops.WriteCount;
        await Assert.That(session.Restore()).IsTrue(); // repeated Stop(): no throw
        await Assert.That(session.Restore()).IsTrue();

        await Assert.That(writesAfterRestore).IsEqualTo(writesAfterEnter + 1);
        await Assert.That(ops.WriteCount).IsEqualTo(writesAfterRestore);
    }

    [Test]
    public async Task Enter_WithoutTty_IsNotRawAndRestoreIsNoOp()
    {
        var ops = new FakeTermiosOps(CookedTermios()) { FailReads = true };
        var session = new RawModeSession(ops);

        await Assert.That(session.Enter()).IsFalse();
        await Assert.That(session.IsRaw).IsFalse();
        await Assert.That(session.Restore()).IsTrue();
        await Assert.That(ops.WriteCount).IsEqualTo(0);
    }

    [Test]
    public async Task Restore_RetriesAfterWriteFailure()
    {
        var ops = new FakeTermiosOps(CookedTermios());
        var session = new RawModeSession(ops);
        session.Enter();

        ops.FailWrites = true;
        await Assert.That(session.Restore()).IsFalse();
        await Assert.That(session.IsRaw).IsTrue(); // still raw → a later Stop retries

        ops.FailWrites = false;
        await Assert.That(session.Restore()).IsTrue();
        await Assert.That(ops.Current).IsEqualTo(CookedTermios());
    }

    [Test]
    public async Task UnixTerminal_StartStop_RestoresTheTty()
    {
        var ops = new FakeTermiosOps(CookedTermios());
        var terminal = new UnixTerminal(ops, new FakeWinsizeProbe(80, 24), runSystemHooks: false);

        terminal.Start(_ => { }, () => { });
        await Assert.That(terminal.IsRawMode).IsTrue();

        terminal.Stop();
        await Assert.That(terminal.IsRawMode).IsFalse();
        await Assert.That(ops.Current.c_lflag & (Icanon | Echo | Isig)).IsEqualTo(Icanon | Echo | Isig);

        terminal.Stop(); // repeated Stop() must not throw
    }

    internal static Termios CookedTermios()
    {
        var termios = default(Termios);
        termios.c_iflag = Icrnl | Ixon | 0x4; // ICRNL|IXON|IGNPAR
        termios.c_oflag = Opost | 0x4; // OPOST|ONLCR
        termios.c_cflag = Csize | Parenb | 0x20 | 0xFF00; // CS5|CSTOPB|baud
        termios.c_lflag = Isig | Icanon | Echo | Iexten;
        termios.c_ispeed = 9600;
        termios.c_ospeed = 9600;
        return termios;
    }
}

/// <summary>In-memory termios surface: records the current tty state like the kernel would.</summary>
internal sealed class FakeTermiosOps : ITermiosOps
{
    internal FakeTermiosOps(Termios initial) => Current = initial;

    internal Termios Current { get; private set; }

    internal int ReadCount { get; private set; }

    internal int WriteCount { get; private set; }

    internal bool FailReads { get; set; }

    internal bool FailWrites { get; set; }

    public bool TryRead(out Termios termios)
    {
        ReadCount++;
        if (FailReads)
        {
            termios = default;
            return false;
        }
        termios = Current;
        return true;
    }

    public bool TryWrite(in Termios termios)
    {
        WriteCount++;
        if (FailWrites)
            return false;
        Current = termios;
        return true;
    }
}

/// <summary>Fake geometry source used by the 04-H5 guards.</summary>
internal sealed class FakeWinsizeProbe : IWinsizeProbe
{
    internal FakeWinsizeProbe(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    internal int Columns { get; set; }

    internal int Rows { get; set; }

    internal bool Available { get; set; } = true;

    public bool TryRead(out int columns, out int rows)
    {
        columns = Columns;
        rows = Rows;
        return Available;
    }
}
