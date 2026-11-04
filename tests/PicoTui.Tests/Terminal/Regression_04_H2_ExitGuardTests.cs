using System.Runtime.InteropServices;

namespace PicoTui.Tests.Terminal;

/// <summary>
/// WP-F5 guard for 04-H2 (High): no exit hook existed, so SIGTERM/SIGHUP (and
/// ProcessExit after a FailFast) left the tty in raw mode with no way to recover.
/// The signal itself cannot be raised here without killing the runner, so the guard
/// exercises the hook the signal and ProcessExit share, plus the symmetric
/// Dispose/Register wiring.
/// <para>
/// F10b: this file used to call <c>SimulateProcessExit()</c>, which invoked the
/// private handler directly and therefore proved nothing about the subscription
/// (R3). The same intents are now driven through an injected
/// <see cref="IProcessExitSource"/> that raises <em>only</em> what was subscribed;
/// the real <see cref="AppDomain.ProcessExit"/> subscription is asserted in
/// <see cref="Regression_F10_H2_ExitGuardWiringTests"/>.
/// </para>
/// </summary>
public sealed class Regression_04_H2_ExitGuardTests
{
    /// <summary>Event source double: raises only when something is subscribed.</summary>
    private sealed class FakeProcessExitSource : IProcessExitSource
    {
        private EventHandler? _handler;

        public void Subscribe(EventHandler handler) => _handler += handler;

        public void Unsubscribe(EventHandler handler) => _handler -= handler;

        internal bool HasHandler => _handler is not null;

        internal void Raise() => _handler?.Invoke(this, EventArgs.Empty);
    }

    [Test]
    public async Task TryRestore_RunsOnceAndIsIdempotent()
    {
        var restores = 0;
        using var guard = new ExitGuard(() => restores++, new FakeProcessExitSource());

        await Assert.That(guard.TryRestore()).IsTrue();
        await Assert.That(guard.TryRestore()).IsFalse();
        await Assert.That(restores).IsEqualTo(1);
    }

    [Test]
    public async Task ProcessExitHook_RestoresTheTerminal()
    {
        var restores = 0;
        var source = new FakeProcessExitSource();
        using var guard = new ExitGuard(() => restores++, source);
        guard.Register();

        await Assert.That(guard.IsRegistered).IsTrue();
        await Assert.That(source.HasHandler).IsTrue();

        source.Raise();
        await Assert.That(restores).IsEqualTo(1);

        source.Raise(); // second exit event: no double restore
        await Assert.That(restores).IsEqualTo(1);
    }

    [Test]
    public async Task DisposeUnregistersTheExitHook()
    {
        var restores = 0;
        var source = new FakeProcessExitSource();
        var guard = new ExitGuard(() => restores++, source);
        guard.Register();
        guard.Dispose();

        await Assert.That(guard.IsRegistered).IsFalse();
        await Assert.That(source.HasHandler).IsFalse();
        source.Raise();
        await Assert.That(restores).IsEqualTo(0); // unregistered → hook cannot fire
    }

    [Test]
    public async Task Register_IsSymmetricAndSafeToRepeat()
    {
        var guard = new ExitGuard(() => { }, new FakeProcessExitSource());
        guard.Register();
        guard.Register();
        await Assert.That(guard.IsRegistered).IsTrue();
        guard.Dispose();
        guard.Dispose(); // symmetric with the double Register: no throw
        await Assert.That(guard.IsRegistered).IsFalse();
    }

    [Test]
    public async Task SignalHook_RestoresTheTerminal()
    {
        // the same handler SIGTERM/SIGINT/SIGHUP/SIGQUIT/SIGTSTP run
        var restores = 0;
        var handles = new List<FakeHandle>();
        var handlers = new Dictionary<PosixSignal, Action<PosixSignalContext>>();
        using var guard = new ExitGuard(
            () => restores++,
            new FakeProcessExitSource(),
            (signal, handler) =>
            {
                handlers[signal] = handler;
                var handle = new FakeHandle();
                handles.Add(handle);
                return handle;
            }
        );
        guard.Register();

        handlers[PosixSignal.SIGTERM](new PosixSignalContext(PosixSignal.SIGTERM));
        await Assert.That(restores).IsEqualTo(1);

        guard.Dispose();
        await Assert.That(handles.All(h => h.Disposed)).IsTrue();
    }

    [Test]
    public async Task UnixTerminal_StartInstallsAndStopRemovesTheExitGuard()
    {
        var terminal = new UnixTerminal(
            new FakeTermiosOps(Regression_04_C2_RawModeRestoreTests.CookedTermios()),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false
        );

        terminal.Start(_ => { }, () => { });
        await Assert.That(terminal.ExitGuardInstalled).IsTrue();

        terminal.Stop();
        await Assert.That(terminal.ExitGuardInstalled).IsFalse();
    }

    [Test]
    public async Task WindowsTerminal_StartInstallsAndStopRemovesTheExitGuard()
    {
        var terminal = new WindowsTerminal(
            new FakeConsoleModeOps(),
            new FakeWinsizeProbe(80, 24),
            runSystemHooks: false
        );

        terminal.Start(_ => { }, () => { });
        await Assert.That(terminal.ExitGuardInstalled).IsTrue();

        terminal.Stop();
        await Assert.That(terminal.ExitGuardInstalled).IsFalse();
    }

    private sealed class FakeHandle : IDisposable
    {
        internal bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
