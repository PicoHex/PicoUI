namespace PicoTui.Tests.Terminal;

/// <summary>
/// F10b guard for the 04-C2 residual R3 found: <c>Start()</c> threw away the return
/// value of <c>RawModeSession.Enter()</c> (and of <c>ConsoleModeSession.Enter()</c>),
/// so a tty that refused the raw switch — no <c>/dev/tty</c>, non-Linux runtime,
/// a failing <c>tcsetattr</c>/<c>SetConsoleMode</c> — produced a session that looked
/// healthy while every keystroke was echoed and line-buffered. Nothing recorded it,
/// nothing told the user, and <c>Stop()</c> restored a snapshot that was never taken.
/// <para>
/// The fix degrades instead of pretending: the failure is recorded on
/// <see cref="TerminalFault"/>-bearing surfaces (<c>ModeFault</c>) and the session
/// stays usable. These guards assert the fault is recorded, is not sticky, and that
/// a healthy switch records nothing.
/// </para>
/// </summary>
public sealed class Regression_F10_C2_ModeEnterFailureTests
{
    private static Termios Cooked() => Regression_04_C2_RawModeRestoreTests.CookedTermios();

    private static UnixTerminal NewUnix(FakeTermiosOps ops, bool runSystemHooks = true) =>
        new(ops, new FakeWinsizeProbe(80, 24), runSystemHooks, _ => new StdinPump(new ScriptedStdinStream(), _ => { }));

    [Test]
    public async Task Unix_HealthyStart_RecordsNoFault()
    {
        using var terminal = NewUnix(new FakeTermiosOps(Cooked()));

        terminal.Start(_ => { }, () => { });

        await Assert.That(terminal.ModeFault).IsNull();
        await Assert.That(terminal.IsRawMode).IsTrue();
        terminal.Stop();
    }

    [Test]
    public async Task Unix_SnapshotUnavailable_IsRecorded_AndTheSessionStaysUsable()
    {
        // no /dev/tty: tcgetattr cannot provide the snapshot Stop() would restore
        var ops = new FakeTermiosOps(Cooked()) { FailReads = true };
        using var terminal = NewUnix(ops);

        terminal.Start(_ => { }, () => { }); // must not throw: degraded, not dead

        var fault = terminal.ModeFault;
        await Assert.That(fault).IsNotNull().Because("a failed Enter() must never be swallowed");
        await Assert.That(fault!.Operation).Contains("raw");
        await Assert.That(fault.Reason).IsNotEmpty();
        await Assert.That(terminal.IsRawMode).IsFalse();

        // degraded but usable: the rest of the contract still holds
        terminal.Write("still writable");
        terminal.Stop();
        await Assert.That(terminal.IsRawMode).IsFalse();
    }

    [Test]
    public async Task Unix_RawWriteFails_IsRecorded()
    {
        // tcsetattr failed: the tty is unchanged, so the session must not claim raw
        var ops = new FakeTermiosOps(Cooked()) { FailWrites = true };
        using var terminal = NewUnix(ops);

        terminal.Start(_ => { }, () => { });

        await Assert.That(terminal.ModeFault).IsNotNull();
        await Assert.That(terminal.ModeFault!.Reason).Contains("tcsetattr");
        await Assert.That(terminal.IsRawMode).IsFalse();
        terminal.Stop();
    }

    [Test]
    public async Task Unix_RestartAfterADegradedStart_ClearsTheFault()
    {
        // the fault describes *this* session, not the terminal's whole life
        var ops = new FakeTermiosOps(Cooked()) { FailReads = true };
        using var terminal = NewUnix(ops);

        terminal.Start(_ => { }, () => { });
        await Assert.That(terminal.ModeFault).IsNotNull();
        terminal.Stop();

        ops.FailReads = false;
        terminal.Start(_ => { }, () => { });

        await Assert.That(terminal.ModeFault).IsNull();
        await Assert.That(terminal.IsRawMode).IsTrue();
        terminal.Stop();
    }

    [Test]
    [NotInParallel("console-error")]
    public async Task Unix_DegradedStart_TellsTheUserOnStderr()
    {
        // "recorded" is only half of it: a silently degraded session looks like a
        // working one until the user notices their keystrokes are echoed.
        var ops = new FakeTermiosOps(Cooked()) { FailReads = true };
        using var terminal = NewUnix(ops);
#pragma warning disable TUnit0055 // captured and restored in the finally below; a captured
        // stderr is the only way to observe that a degraded start is announced at all
        var original = Console.Error;
        var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            terminal.Start(_ => { }, () => { });
        }
        finally
        {
            Console.SetError(original);
        }
#pragma warning restore TUnit0055

        var warning = captured.ToString();
        await Assert
            .That(warning)
            .Contains("raw mode")
            .Because("a degraded start must be reported, not swallowed");
        terminal.Stop();
    }

    [Test]
    public async Task Windows_HealthyStart_RecordsNoFault()
    {
        using var terminal = new WindowsTerminal(
            new FakeConsoleModeOps(),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false
        );

        terminal.Start(_ => { }, () => { });

        await Assert.That(terminal.ModeFault).IsNull();
        await Assert.That(terminal.IsVtModeActive).IsTrue();
        terminal.Stop();
    }

    [Test]
    public async Task Windows_SetConsoleModeFails_IsRecorded()
    {
        var console = new FakeConsoleModeOps { FailSet = true };
        using var terminal = new WindowsTerminal(
            console,
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false
        );

        terminal.Start(_ => { }, () => { });

        var fault = terminal.ModeFault;
        await Assert.That(fault).IsNotNull().Because("the Enter() result must not be discarded");
        await Assert.That(fault!.Operation).Contains("console");
        await Assert.That(terminal.SetModeFailures).IsGreaterThan(0);
        terminal.Stop();
    }

    [Test]
    [NotInParallel("console-error")]
    public async Task Windows_DegradedStart_TellsTheUserOnStderr()
    {
        var console = new FakeConsoleModeOps { FailSet = true };
        using var terminal = new WindowsTerminal(
            console,
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: true,
            _ => new StdinPump(new ScriptedStdinStream(), _ => { })
        );
#pragma warning disable TUnit0055 // captured and restored in the finally below
        var original = Console.Error;
        var captured = new StringWriter();
        try
        {
            Console.SetError(captured);
            terminal.Start(_ => { }, () => { });
        }
        finally
        {
            Console.SetError(original);
        }
#pragma warning restore TUnit0055

        await Assert.That(captured.ToString()).Contains("console mode");
        terminal.Stop();
    }
}
