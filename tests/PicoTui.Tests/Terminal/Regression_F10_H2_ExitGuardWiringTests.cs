#pragma warning disable CA1416 // These guards NAME SIGTSTP/SIGHUP to assert how a platform gap
// is reported (RequiredSignals/FailedSignals). They never call Create with them, and the
// production filter — not a test — is what keeps Windows off those signals.
using System.Reflection;
using System.Runtime.InteropServices;

namespace PicoTui.Tests.Terminal;

/// <summary>
/// F10b guard for the 04-H2 residual that R3 proved empirically: the F5 guard was
/// not a guard at all.
/// <list type="bullet">
/// <item><c>IsRegistered</c> was a plain bool set <em>before</em> the registration
/// loop, so it said "registered" even when every <c>Create</c> threw.</item>
/// <item><c>SimulateProcessExit()</c> invoked the private handler <em>directly</em>,
/// bypassing <see cref="AppDomain.ProcessExit"/> — so deleting the
/// <c>ProcessExit +=</c> line (or making every <c>PosixSignalRegistration.Create</c>
/// throw) kept every test green.</item>
/// </list>
/// <para>
/// The guards below observe the real wiring instead: the signal set that actually
/// registered (with live handles), the failures that actually happened, and the
/// handler that is really attached to <see cref="AppDomain.ProcessExit"/> (located by
/// reflection and invoked through the runtime's own handler list, never called
/// directly).
/// </para>
/// <para>
/// Windows note: the restore set is declared the same way on every OS
/// (<see cref="ExitGuard.RequestedSignals"/>), but Windows has no
/// <c>SIGHUP</c>/<c>SIGTSTP</c> — <c>PosixSignalRegistration.Create</c> throws
/// <see cref="PlatformNotSupportedException"/> for them, so they land in
/// <see cref="ExitGuard.FailedSignals"/> and the guard skips them explicitly instead
/// of pretending they were armed. <c>SIGQUIT</c> (CTRL_BREAK) and <c>SIGTERM</c> are
/// supported by Windows and are asserted there too.
/// </para>
/// </summary>
public sealed class Regression_F10_H2_ExitGuardWiringTests
{
    // ---------------------------------------------------------------- helpers

    /// <summary>Records subscriptions and raises them only when subscribed — the
    /// "event source" seam that replaces the old direct handler call.</summary>
    private sealed class FakeProcessExitSource : IProcessExitSource
    {
        private EventHandler? _handler;

        internal int Subscribes;
        internal int Unsubscribes;

        internal bool HasHandler => _handler is not null;

        public void Subscribe(EventHandler handler)
        {
            Subscribes++;
            _handler += handler;
        }

        public void Unsubscribe(EventHandler handler)
        {
            Unsubscribes++;
            _handler -= handler;
        }

        internal void Raise() => _handler?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Signal registry double: records (signal, handle) and the handler the
    /// guard installed, so a test can drive it like the runtime would.</summary>
    private sealed class FakeRegistrar
    {
        internal readonly List<PosixSignal> Registered = [];
        internal readonly List<string> Disposed = [];
        internal readonly Dictionary<PosixSignal, Action<PosixSignalContext>> Handlers = [];
        internal readonly HashSet<PosixSignal> Throw = [];

        internal IDisposable? Create(PosixSignal signal, Action<PosixSignalContext> handler)
        {
            if (Throw.Contains(signal))
                throw new PlatformNotSupportedException($"{signal} is not supported here (fake)");
            Registered.Add(signal);
            Handlers[signal] = handler;
            return new Handle(signal, Disposed);
        }

        internal void Fire(PosixSignal signal)
        {
            var context = new PosixSignalContext(signal);
            Handlers[signal](context);
            LastContext = context;
        }

        internal PosixSignalContext? LastContext { get; private set; }

        private sealed class Handle(PosixSignal signal, List<string> disposed) : IDisposable
        {
            public void Dispose() => disposed.Add(signal.ToString());
        }
    }

    // ------------------------------------------------- 1. the restore-set truth

    [Test]
    public async Task RequestedSignals_IncludeSigquitAndSigtstp()
    {
        // F10b (04-H2 residual): SIGQUIT (Ctrl-\) and SIGTSTP (Ctrl-Z) killed the
        // process with the tty still raw — neither was hooked.
        var requested = ExitGuard.RequestedSignals;

        await Assert.That(requested).Contains(PosixSignal.SIGQUIT);
        await Assert.That(requested).Contains(PosixSignal.SIGTSTP);
        await Assert.That(requested).Contains(PosixSignal.SIGTERM);
        await Assert.That(requested).Contains(PosixSignal.SIGINT);
        await Assert.That(requested).Contains(PosixSignal.SIGHUP);
    }

    [Test]
    public async Task RegisteredSignals_ContainThePlatformRestoreSet_WithLiveHandles()
    {
        var restores = 0;
        using var guard = new ExitGuard(() => restores++, new FakeProcessExitSource());
        guard.Register();

        var required = ExitGuard.RequiredSignals;
        foreach (var signal in required)
        {
            await Assert
                .That(guard.RegisteredSignals)
                .Contains(signal)
                .Because($"the restore set must contain {signal} on {Environment.OSVersion.Platform}");
        }

        // The handles are what prove the registration happened — a bool could lie.
        await Assert.That(guard.Handles.Count).IsEqualTo(required.Count);
        await Assert.That(guard.Handles.All(h => h is not null)).IsTrue();
        await Assert.That(guard.IsRegistered).IsTrue();
        await Assert.That(guard.Defect).IsNull();
    }

    [Test]
    public async Task PlatformUnavailableSignal_IsNotRequired_AndIsDocumented()
    {
        // Documented platform split, asserted rather than skipped silently.
        // Measured on this runtime: SIGTERM/SIGINT/SIGHUP/SIGQUIT register on Windows
        // too (console ctrl/close events); only SIGTSTP throws
        // PlatformNotSupportedException there (no job control on the Windows console).
        var required = ExitGuard.RequiredSignals;
        if (OperatingSystem.IsWindows())
        {
            await Assert
                .That(required.Contains(PosixSignal.SIGTSTP))
                .IsFalse()
                .Because("Windows has no SIGTSTP — PosixSignalRegistration.Create throws "
                    + "PlatformNotSupportedException; the restore set still *declares* it "
                    + "(RequestedSignals), RequiredSignals just cannot arm it");
            // the signals Windows does support are required there too
            await Assert.That(required.Contains(PosixSignal.SIGQUIT)).IsTrue();
            await Assert.That(required.Contains(PosixSignal.SIGHUP)).IsTrue();
            await Assert.That(required.Contains(PosixSignal.SIGTERM)).IsTrue();
            await Assert.That(required.Contains(PosixSignal.SIGINT)).IsTrue();
        }
        else
        {
            await Assert.That(required.Contains(PosixSignal.SIGHUP)).IsTrue();
            await Assert.That(required.Contains(PosixSignal.SIGTSTP)).IsTrue();
        }

        using var guard = new ExitGuard(() => { }, new FakeProcessExitSource());
        guard.Register();
        // Whatever the OS does not offer must be *observed* as a failure, not hidden.
        foreach (var signal in ExitGuard.RequestedSignals.Except(required))
        {
            await Assert
                .That(guard.RegisteredSignals.Contains(signal))
                .IsFalse()
                .Because($"{signal} must not be claimed as armed on this platform");
        }
    }

    [Test]
    public async Task SignalHandler_Restores_AndLetsTheDefaultActionTerminate()
    {
        var restores = 0;
        var registrar = new FakeRegistrar();
        using var guard = new ExitGuard(() => restores++, new FakeProcessExitSource(), registrar.Create);
        guard.Register();

        registrar.Fire(PosixSignal.SIGTERM);

        await Assert.That(restores).IsEqualTo(1);
        await Assert
            .That(registrar.LastContext!.Cancel)
            .IsFalse()
            .Because("the hook restores and then lets the process die, it does not swallow the signal");
    }

    // ------------------------------------- 2. registration failure is observable

    [Test]
    public async Task RegistrationFailure_IsRecorded_AndIsRegisteredStaysFalse()
    {
        var restores = 0;
        var registrar = new FakeRegistrar();
        foreach (var signal in ExitGuard.RequestedSignals)
            registrar.Throw.Add(signal);

        using var guard = new ExitGuard(() => restores++, new FakeProcessExitSource(), registrar.Create);
        guard.Register();

        // The pre-F10b guard reported "registered" here, because the bool was set
        // before the loop that can throw.
        await Assert.That(guard.RegisteredSignals.Count).IsEqualTo(0);
        await Assert.That(guard.FailedSignals.Count).IsEqualTo(ExitGuard.RequestedSignals.Count);
        await Assert.That(guard.Handles.Count).IsEqualTo(0);
        await Assert
            .That(guard.IsRegistered)
            .IsFalse()
            .Because("nothing registered — the guard must report the defect, not a bool");
        await Assert.That(guard.Defect).IsNotNull();
    }

    [Test]
    public async Task PartialRegistrationFailure_DoesNotAbortTheRemainingSignals()
    {
        // One unavailable signal must not stop the rest from arming (the pre-F10b code
        // caught PlatformNotSupportedException per signal, and that part was right).
        var failing = PosixSignal.SIGTSTP;

        var registrar = new FakeRegistrar();
        registrar.Throw.Add(failing);

        using var guard = new ExitGuard(() => { }, new FakeProcessExitSource(), registrar.Create);
        guard.Register();

        await Assert.That(guard.FailedSignals).Contains(failing);
        foreach (var signal in ExitGuard.RequestedSignals.Where(s => s != failing))
            await Assert.That(guard.RegisteredSignals).Contains(signal);

        // Whether the guard is still "armed" depends on whether the failed signal is
        // required on this OS: SIGTSTP is not required on Windows, so there the guard
        // must still report healthy (a platform gap is not a defect); on Unix it is.
        var stillRequired = ExitGuard.RequiredSignals.Contains(failing);
        await Assert.That(guard.IsRegistered).IsEqualTo(!stillRequired);
        if (stillRequired)
            await Assert.That(guard.Defect).Contains("SIGTSTP");
    }

    [Test]
    public async Task MissingASentinelSignal_IsADefect()
    {
        // SIGTERM is what `kill` and `docker stop` send: without it the guard is a
        // placebo even when the other signals armed fine.
        var registrar = new FakeRegistrar();
        registrar.Throw.Add(PosixSignal.SIGTERM);

        using var guard = new ExitGuard(() => { }, new FakeProcessExitSource(), registrar.Create);
        guard.Register();

        await Assert.That(guard.IsRegistered).IsFalse();
        await Assert.That(guard.Defect).Contains("SIGTERM");
    }

    // ------------------------- 3. the real AppDomain.ProcessExit subscription

    [Test]
    public async Task Register_ReallyAttachesTheHandlerToAppDomainProcessExit()
    {
        var restores = 0;
        var guard = new ExitGuard(() => restores++);
        var before = AppDomainProcessExitProbe.CountFor(guard);

        guard.Register();

        // MUTATION TARGET: comment out `AppDomain.CurrentDomain.ProcessExit += handler`
        // and this count stays 0 — the old guard could not see that, because it called
        // the handler directly.
        await Assert
            .That(AppDomainProcessExitProbe.CountFor(guard))
            .IsEqualTo(before + 1)
            .Because("the handler must be attached to AppDomain.ProcessExit itself");

        guard.Dispose();
        await Assert.That(AppDomainProcessExitProbe.CountFor(guard)).IsEqualTo(before);
    }

    [Test]
    public async Task AttachedProcessExitHandler_RestoresTheTerminal()
    {
        var restores = 0;
        var guard = new ExitGuard(() => restores++);
        guard.Register();

        // Fire the handler the runtime holds — not a test-only shortcut.
        var fired = AppDomainProcessExitProbe.Fire(guard);

        await Assert.That(fired).IsEqualTo(1);
        await Assert.That(restores).IsEqualTo(1);
        AppDomainProcessExitProbe.Fire(guard); // second exit: TryRestore is idempotent
        await Assert.That(restores).IsEqualTo(1);
        guard.Dispose();
    }

    [Test]
    public async Task Dispose_UnsubscribesProcessExit_AndReleasesEveryHandle()
    {
        var restores = 0;
        var source = new FakeProcessExitSource();
        var registrar = new FakeRegistrar();
        var guard = new ExitGuard(() => restores++, source, registrar.Create);
        guard.Register();
        await Assert.That(source.HasHandler).IsTrue();

        guard.Dispose();

        await Assert.That(source.HasHandler).IsFalse();
        await Assert.That(registrar.Disposed.Count).IsEqualTo(registrar.Registered.Count);
        await Assert.That(guard.Handles.Count).IsEqualTo(0);
        await Assert.That(guard.IsRegistered).IsFalse();

        source.Raise(); // nothing is subscribed any more
        await Assert.That(restores).IsEqualTo(0);
        guard.Dispose(); // idempotent
    }

    // ------------------------------------------- 4. the terminals use the guard

    [Test]
    public async Task UnixTerminal_GuardIsComplete_WithTheRealRegistration()
    {
        var terminal = new UnixTerminal(
            new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false
        );

        terminal.Start(_ => { }, () => { });
        try
        {
            await Assert.That(terminal.ExitGuardInstalled).IsTrue();
        }
        finally
        {
            terminal.Stop();
        }
    }

    [Test]
    public async Task UnixTerminal_ReportsAMissingGuard_WhenNoSignalCanBeRegistered()
    {
        // MUTATION TARGET: make every PosixSignalRegistration.Create throw. The old
        // `IsRegistered` bool was set before the loop, so this stayed green.
        var registrar = new FakeRegistrar();
        foreach (var signal in ExitGuard.RequestedSignals)
            registrar.Throw.Add(signal);

        var terminal = new UnixTerminal(
            new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false,
            StdinPump.ForConsole,
            restore => new ExitGuard(restore, new FakeProcessExitSource(), registrar.Create)
        );

        terminal.Start(_ => { }, () => { });
        try
        {
            await Assert.That(terminal.ExitGuardInstalled).IsFalse();
        }
        finally
        {
            terminal.Stop();
        }
    }

    [Test]
    public async Task WindowsTerminal_GuardIsComplete_WithTheRealRegistration()
    {
        var terminal = new WindowsTerminal(
            new FakeConsoleModeOps(),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false
        );

        terminal.Start(_ => { }, () => { });
        try
        {
            await Assert.That(terminal.ExitGuardInstalled).IsTrue();
        }
        finally
        {
            terminal.Stop();
        }
    }

    [Test]
    public async Task ProcessExitThroughTheInjectedSource_RestoresTheTtyAndThenStopIsSilent()
    {
        // End to end: the restore the guard runs is the same idempotent one Stop()
        // uses, so an abnormal exit cannot leave the tty raw and a later Stop() must
        // not re-write the snapshot (F8 restore ordering is preserved).
        var ops = new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios());
        var source = new FakeProcessExitSource();
        var terminal = new UnixTerminal(
            ops,
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false,
            StdinPump.ForConsole,
            restore => new ExitGuard(restore, source)
        );

        terminal.Start(_ => { }, () => { });
        await Assert.That(terminal.IsRawMode).IsTrue();

        source.Raise(); // the process is dying without ExitAsync

        await Assert.That(terminal.IsRawMode).IsFalse();
        await Assert.That(ops.Current).IsEqualTo(Regression_04_C2_RawModeRestoreTests.CookedTermios());
        var writes = ops.WriteCount;
        terminal.Stop();
        await Assert.That(ops.WriteCount).IsEqualTo(writes);
    }
}

/// <summary>
/// Reads <see cref="AppDomain.ProcessExit"/>'s own handler list through reflection.
/// There is no public API for it, and this is the only way to prove the guard is
/// subscribed to the runtime event instead of calling its handler directly — the
/// exact hole R3 found in the F5 guard.
/// </summary>
internal static class AppDomainProcessExitProbe
{
    private static readonly FieldInfo Field =
        typeof(AppDomain)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(f =>
                typeof(Delegate).IsAssignableFrom(f.FieldType)
                && f.Name.Contains("exit", StringComparison.OrdinalIgnoreCase)
            )
        ?? throw new InvalidOperationException(
            "cannot locate AppDomain's ProcessExit handler field — the F10b guard needs it "
                + "to prove the subscription is real. Fields seen: "
                + string.Join(
                    ", ",
                    typeof(AppDomain)
                        .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Select(f => $"{f.Name}:{f.FieldType.Name}")
                )
        );

    private static Delegate[] Handlers()
    {
        if (Field.GetValue(AppDomain.CurrentDomain) is not Delegate d)
            return [];
        return d.GetInvocationList();
    }

    /// <summary>Number of handlers attached whose target is <paramref name="owner"/>.</summary>
    internal static int CountFor(object owner) =>
        Handlers().Count(h => ReferenceEquals(h.Target, owner));

    /// <summary>Invokes exactly the handlers owned by <paramref name="owner"/> — the
    /// ones the runtime would run on process exit. Returns how many ran.</summary>
    internal static int Fire(object owner)
    {
        var mine = Handlers().Where(h => ReferenceEquals(h.Target, owner)).ToArray();
        foreach (var handler in mine)
        {
            if (handler is EventHandler typed)
                typed(AppDomain.CurrentDomain, EventArgs.Empty);
            else
                handler.DynamicInvoke(AppDomain.CurrentDomain, EventArgs.Empty);
        }
        return mine.Length;
    }
}
