#pragma warning disable CA1416 // These guards NAME SIGWINCH to assert how the wiring and the
// platform gap are reported. Naming a signal is not calling a Windows-unsupported API: the
// production registrar refuses on Windows before any registration happens, and the injected
// registrar below is the whole point — it has to be consulted on every OS.
using System.Runtime.InteropServices;

namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-H5 (High): geometry was read once in the constructor and
/// <c>onResize</c> never fired, so a resized window kept the startup dimensions.
/// Injecting a changing fake size must move <c>Columns</c>/<c>Rows</c>.
/// <para>
/// F13 — the SIGWINCH half of this guard was a placebo. It asserted
/// <c>terminal.ResizeSignalRegistered == OperatingSystem.IsLinux()</c>, and because
/// <see cref="UnixTerminal"/> returned early from the registration on Windows, BOTH sides
/// were <see langword="false"/> there: deleting the registration kept the suite green (the
/// original comment admitted there was "no Linux runner here").
/// </para>
/// <para>
/// The fix is the one F10b applied to <see cref="ExitGuard"/>: the registration is no longer
/// an invisible side effect. <see cref="TerminalGeometry"/> takes a <c>SignalRegistrar</c>,
/// records what actually registered
/// (<see cref="TerminalGeometry.RegisteredResizeSignals"/>), and a test can inject a
/// registrar that exists on every OS — so the wiring is asserted on Windows too, while the
/// real <c>PosixSignalRegistration</c> path is asserted where SIGWINCH exists.
/// </para>
/// </summary>
public sealed class Regression_04_H5_GeometryTests
{
    [Test]
    public async Task Geometry_RefreshReportsChange()
    {
        var probe = new FakeWinsizeProbe(100, 30);
        var geometry = new TerminalGeometry(probe);

        await Assert.That(geometry.Refresh()).IsTrue(); // 80x24 default → 100x30
        await Assert.That(geometry.Columns).IsEqualTo(100);
        await Assert.That(geometry.Rows).IsEqualTo(30);

        probe.Columns = 120;
        probe.Rows = 40;
        await Assert.That(geometry.Refresh()).IsTrue();
        await Assert.That(geometry.Columns).IsEqualTo(120);
        await Assert.That(geometry.Rows).IsEqualTo(40);

        await Assert.That(geometry.Refresh()).IsFalse(); // unchanged
    }

    [Test]
    public async Task Geometry_UnavailableProbe_KeepsPreviousSize()
    {
        var probe = new FakeWinsizeProbe(100, 30);
        var geometry = new TerminalGeometry(probe);
        geometry.Refresh();

        probe.Available = false;
        await Assert.That(geometry.Refresh()).IsFalse();
        await Assert.That(geometry.Columns).IsEqualTo(100);
        await Assert.That(geometry.Rows).IsEqualTo(30);
    }

    [Test]
    public async Task UnixTerminal_ResizeRefreshesColumnsRowsAndCallback()
    {
        var probe = new FakeWinsizeProbe(80, 24);
        var terminal = new UnixTerminal(
            new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios()),
            probe,
            runSystemHooks: false
        );

        var resizes = 0;
        terminal.Start(_ => { }, () => resizes++);

        probe.Columns = 132;
        probe.Rows = 43;
        await Assert.That(terminal.RefreshGeometry()).IsTrue();

        await Assert.That(terminal.Columns).IsEqualTo(132);
        await Assert.That(terminal.Rows).IsEqualTo(43);
        await Assert.That(resizes).IsEqualTo(1);

        await Assert.That(terminal.RefreshGeometry()).IsFalse(); // same size → no callback
        await Assert.That(resizes).IsEqualTo(1);

        terminal.Stop();
    }

    [Test]
    public async Task WindowsTerminal_ResizeRefreshesColumnsRowsAndCallback()
    {
        var probe = new FakeWinsizeProbe(80, 24);
        var terminal = new WindowsTerminal(
            new FakeConsoleModeOps(),
            probe,
            runSystemHooks: false
        );

        var resizes = 0;
        terminal.Start(_ => { }, () => resizes++);

        probe.Columns = 200;
        probe.Rows = 50;
        await Assert.That(terminal.RefreshGeometry()).IsTrue();

        await Assert.That(terminal.Columns).IsEqualTo(200);
        await Assert.That(terminal.Rows).IsEqualTo(50);
        await Assert.That(resizes).IsEqualTo(1);

        terminal.Stop();
    }

    // ────────────────────────────── the SIGWINCH wiring (F13: a real guard)

    /// <summary>
    /// MUTATION TARGET (F13): delete the
    /// <c>_resizeSignalRegistrar(PosixSignal.SIGWINCH, …)</c> call in
    /// <see cref="TerminalGeometry.RegisterResizeSignal"/> and this goes red on EVERY OS —
    /// the injected registrar is the witness and it is never null in this test.
    /// </summary>
    [Test]
    public async Task UnixTerminal_ResizeHookIsWiredOnStarting()
    {
        var registrar = new FakeSignalRegistrar();
        var terminal = NewUnixTerminal(new FakeWinsizeProbe(80, 24), registrar);

        terminal.Start(_ => { }, () => { });
        try
        {
            await Assert
                .That(terminal.ResizeSignals)
                .Contains(PosixSignal.SIGWINCH)
                .Because("SIGWINCH must be registered when the terminal starts");
            await Assert.That(terminal.ResizeSignalRegistered).IsTrue();
            // the registrar is the independent witness — not the terminal's own bool
            await Assert.That(registrar.Registered).Contains(PosixSignal.SIGWINCH);
            await Assert.That(registrar.CreateCount).IsEqualTo(1);
        }
        finally
        {
            terminal.Stop();
        }

        // Stop() must release the hook, or a restarted session leaks one per Start()
        await Assert.That(terminal.ResizeSignalRegistered).IsFalse();
        await Assert.That(registrar.Disposed).Contains(PosixSignal.SIGWINCH);
    }

    /// <summary>The registered handler is the resize path, not a no-op: firing it through
    /// the registrar must move the geometry and fire the callback exactly once.</summary>
    [Test]
    public async Task UnixTerminal_WinchHook_RefreshesGeometryAndFiresTheCallback()
    {
        var probe = new FakeWinsizeProbe(80, 24);
        var registrar = new FakeSignalRegistrar();
        var terminal = NewUnixTerminal(probe, registrar);

        var resizes = 0;
        terminal.Start(_ => { }, () => resizes++);
        try
        {
            probe.Columns = 132;
            probe.Rows = 43;

            var context = registrar.Fire(PosixSignal.SIGWINCH);

            await Assert.That(terminal.Columns).IsEqualTo(132);
            await Assert.That(terminal.Rows).IsEqualTo(43);
            await Assert.That(resizes).IsEqualTo(1);
            await Assert
                .That(context.Cancel)
                .IsTrue()
                .Because("the handler keeps the runtime's default (ignore) out of the way");

            // same size again: the hook must not spam the renderer
            registrar.Fire(PosixSignal.SIGWINCH);
            await Assert.That(resizes).IsEqualTo(1);
        }
        finally
        {
            terminal.Stop();
        }
    }

    /// <summary>
    /// The REAL registration, asserted where SIGWINCH exists. Everywhere else this is an
    /// explicit skip (not a silently-empty assertion): the Windows console has no resize
    /// signal at all, so there is nothing to register and the frame loop calls
    /// <see cref="UnixTerminal.RefreshGeometry"/> instead.
    /// </summary>
    [Test]
    [UnixSignalOnly]
    public async Task UnixTerminal_RealWinchRegistration_IsLive()
    {
        var terminal = NewUnixTerminal(new FakeWinsizeProbe(80, 24), registrar: null);

        terminal.Start(_ => { }, () => { });
        try
        {
            await Assert.That(terminal.ResizeSignals).Contains(PosixSignal.SIGWINCH);
            await Assert.That(terminal.ResizeSignalRegistered).IsTrue();
        }
        finally
        {
            terminal.Stop();
        }

        await Assert.That(terminal.ResizeSignalRegistered).IsFalse();
    }

    /// <summary>A registrar that cannot register (no SIGWINCH in this runtime) must be
    /// REPORTED as a gap, never counted as armed — the guard surface has to be able to say
    /// "no".</summary>
    [Test]
    public async Task TerminalGeometry_UnregisterableSignal_IsReportedAsNotArmed()
    {
        var registrar = new FakeSignalRegistrar();
        registrar.ThrowOn(PosixSignal.SIGWINCH);
        var geometry = new TerminalGeometry(new FakeWinsizeProbe(80, 24), registrar.Create);

        await Assert.That(geometry.RegisterResizeSignal(_ => { })).IsFalse();
        await Assert.That(geometry.RegisteredResizeSignals.Count).IsEqualTo(0);
        await Assert.That(geometry.ResizeSignalRegistered).IsFalse();
        await Assert.That(registrar.Registered.Count).IsEqualTo(0);
    }

    private static UnixTerminal NewUnixTerminal(
        FakeWinsizeProbe probe,
        FakeSignalRegistrar? registrar
    ) =>
        new(
            new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios()),
            probe,
            runSystemHooks: false,
            StdinPump.ForConsole,
            exitGuardFactory: null,
            resizeSignalRegistrar: registrar is null ? null : registrar.Create
        );

    /// <summary>
    /// Signal registry double — the F10b <c>FakeRegistrar</c> shape applied to SIGWINCH: it
    /// records what was registered, keeps the handler the terminal installed, and can be
    /// made to fail so "armed" cannot be faked.
    /// </summary>
    private sealed class FakeSignalRegistrar
    {
        private readonly List<PosixSignal> _registered = [];
        private readonly List<PosixSignal> _disposed = [];
        private readonly Dictionary<PosixSignal, Action<PosixSignalContext>> _handlers = [];
        private readonly HashSet<PosixSignal> _throw = [];

        internal IReadOnlyList<PosixSignal> Registered => _registered;

        internal IReadOnlyList<PosixSignal> Disposed => _disposed;

        internal int CreateCount { get; private set; }

        internal void ThrowOn(PosixSignal signal) => _throw.Add(signal);

        internal IDisposable? Create(PosixSignal signal, Action<PosixSignalContext> handler)
        {
            CreateCount++;
            if (_throw.Contains(signal))
                throw new PlatformNotSupportedException($"{signal} is not supported here (fake)");
            _registered.Add(signal);
            _handlers[signal] = handler;
            return new Handle(signal, _disposed);
        }

        /// <summary>Runs the handler the terminal installed, exactly as the runtime would.</summary>
        internal PosixSignalContext Fire(PosixSignal signal)
        {
            var context = new PosixSignalContext(signal);
            _handlers[signal](context);
            return context;
        }

        private sealed class Handle(PosixSignal signal, List<PosixSignal> disposed) : IDisposable
        {
            public void Dispose() => disposed.Add(signal);
        }
    }
}

/// <summary>
/// Marks a test as meaningful only where a Unix signal exists. On Windows (and anywhere
/// SIGWINCH does not exist) the test is SKIPPED with this reason instead of comparing two
/// constants and passing — the exact defect F13 fixes in this file.
/// </summary>
internal sealed class UnixSignalOnlyAttribute()
    : SkipAttribute(
        "SIGWINCH only exists on Unix. The Windows console has no resize signal, so "
            + "UnixTerminal registers none there and the frame loop calls "
            + "RefreshGeometry() instead. The injected-registrar guards "
            + "(UnixTerminal_ResizeHookIsWiredOnStarting and "
            + "UnixTerminal_WinchHook_RefreshesGeometryAndFiresTheCallback) cover the wiring "
            + "on this OS."
    )
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context) =>
        Task.FromResult(!OperatingSystem.IsLinux());
}
